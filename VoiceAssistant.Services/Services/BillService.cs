using System.Text.Json;
using System.Text.RegularExpressions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Data;
using Microsoft.Extensions.Configuration;

namespace VoiceAssistant.Services.Services;

public class BillService : IBillService
{
    private readonly IOCRService _ocr;
    private readonly ILLMService _llm;
    private readonly IInventoryRepository _repo;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public BillService(IOCRService ocr, ILLMService llm, IInventoryRepository repo, AppDbContext db, IConfiguration config)
    {
        _ocr = ocr;
        _llm = llm;
        _repo = repo;
        _db = db;
        _config = config;
    }

    private const string BillPrompt =
        "Parse this grocery bill into structured JSON. " +
        "Return ONLY a JSON object with this exact format, no other text:\n" +
        "{\n" +
        "  \"store\": \"store name or Unknown\",\n" +
        "  \"date\": \"YYYY-MM-DD or today\",\n" +
        "  \"items\": [\n" +
        "    {\"name\": \"item name\", \"qty\": 1.0, \"unit\": \"kg/L/units\", \"price\": 0.0}\n" +
        "  ]\n" +
        "}";

    public async Task<ParsedBill> ScanBillAsync(byte[] imageData, CancellationToken ct = default)
    {
        var mode = _config["AssistantSettings:BillScanMode"] ?? "ocr";

        if (mode == "vision")
        {
            var llmResponse = await _llm.ChatWithImageAsync(BillPrompt, imageData, ct);
            return ParseLLMResponse(llmResponse, 0.9f);
        }
        else
        {
            var ocrResult = await _ocr.ExtractTextAsync(imageData, ct);
            var prompt = BillPrompt + "\n\nOCR text:\n" + ocrResult.FullText;
            var llmResponse = await _llm.ChatAsync(prompt, null, ct);
            return ParseLLMResponse(llmResponse, ocrResult.OverallConfidence);
        }
    }

    private ParsedBill ParseLLMResponse(string llmResponse, float ocrConfidence)
    {
        try
        {
            // Strip markdown fences
            var json = llmResponse;
            json = Regex.Replace(json, @"```json\s*", "");
            json = Regex.Replace(json, @"```\s*", "");

            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start == -1 || end == -1) throw new Exception("No JSON found");
            json = json.Substring(start, end - start + 1);

            var doc = JsonDocument.Parse(json);
            var store = doc.RootElement.GetProperty("store").GetString() ?? "Unknown";

            DateTime date = DateTime.UtcNow;
            if (doc.RootElement.TryGetProperty("date", out var dateProp))
                DateTime.TryParse(dateProp.GetString(), out date);

            var items = doc.RootElement.GetProperty("items").EnumerateArray()
                .Select(i => new ParsedBillItem(
                    InventoryService.NormaliseName(i.GetProperty("name").GetString() ?? "Unknown"),
                    i.TryGetProperty("unit", out var u) ? u.GetString() ?? "units" : "units",
                    i.TryGetProperty("qty", out var q) ? q.GetDecimal() : 1,
                    i.TryGetProperty("price", out var p) ? p.GetDecimal() : 0,
                    ocrConfidence
                )).ToList();

            return new ParsedBill(store, date, items);
        }
        catch
        {
            return new ParsedBill("Unknown", DateTime.UtcNow, new List<ParsedBillItem>());
        }
    }

    public async Task SaveBillAsync(ParsedBill bill, string imagePath, CancellationToken ct = default)
    {
        // Save bill record
        var billEntity = new Bill
        {
            Date = bill.Date,
            Store = bill.Store,
            ImagePath = imagePath,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bills.Add(billEntity);
        await _db.SaveChangesAsync(ct);

        // Save each item and update inventory
        foreach (var parsedItem in bill.Items)
        {
            var item = await _repo.GetOrCreateItemAsync(parsedItem.Name, parsedItem.Unit, ct);

            var billItem = new BillItem
            {
                BillId = billEntity.Id,
                ItemId = item.Id,
                Qty = parsedItem.Qty,
                Price = parsedItem.Price,
                Confidence = parsedItem.Confidence,
                ParsedAt = DateTime.UtcNow
            };
            _db.BillItems.Add(billItem);

            // Update inventory with purchased quantity
            await _repo.UpdateStockAsync(item.Id, parsedItem.Qty, EventSource.BillScan,
                $"Purchased from {bill.Store}", ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
