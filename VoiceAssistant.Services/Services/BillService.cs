using System.Text.Json;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using VoiceAssistant.Data;
using Microsoft.EntityFrameworkCore;

namespace VoiceAssistant.Services.Services;

public class BillService : IBillService
{
    private readonly IOCRService _ocr;
    private readonly ILLMService _llm;
    private readonly IInventoryRepository _repo;
    private readonly AppDbContext _db;

    public BillService(IOCRService ocr, ILLMService llm, IInventoryRepository repo, AppDbContext db)
    {
        _ocr = ocr;
        _llm = llm;
        _repo = repo;
        _db = db;
    }

    public async Task<ParsedBill> ScanBillAsync(byte[] imageData, CancellationToken ct = default)
    {
        // Step 1 — OCR
        var ocrResult = await _ocr.ExtractTextAsync(imageData, ct);

        // Step 2 — LLM parses OCR text into structured items
        var prompt = "Parse this grocery bill OCR text into structured JSON. " +
                     "Return ONLY a JSON object with this exact format, no other text:\n" +
                     "{\n" +
                     "  \"store\": \"store name or Unknown\",\n" +
                     "  \"date\": \"YYYY-MM-DD or today\",\n" +
                     "  \"items\": [\n" +
                     "    {\"name\": \"item name\", \"qty\": 1.0, \"unit\": \"kg/L/units\", \"price\": 0.0}\n" +
                     "  ]\n" +
                     "}\n\n" +
                     "OCR text:\n" + ocrResult.FullText;

        var llmResponse = await _llm.ChatAsync(prompt, null, jsonMode: true, ct);

        // Step 3 — parse LLM response
        return ParseLLMResponse(llmResponse, ocrResult.OverallConfidence);
    }

    private ParsedBill ParseLLMResponse(string llmResponse, float ocrConfidence)
    {
        try
        {
            var doc = JsonDocument.Parse(llmResponse);
            var store = doc.RootElement.GetProperty("store").GetString() ?? "Unknown";

            DateTime date = DateTime.UtcNow;
            if (doc.RootElement.TryGetProperty("date", out var dateProp))
                DateTime.TryParse(dateProp.GetString(), out date);

            var items = doc.RootElement.GetProperty("items").EnumerateArray()
                .Select(i => new ParsedBillItem(
                    i.GetProperty("name").GetString() ?? "Unknown",
                    i.TryGetProperty("unit",  out var u) ? u.GetString() ?? "units" : "units",
                    i.TryGetProperty("qty",   out var q) ? q.GetDecimal() : 1,
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
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var billEntity = new Bill
            {
                Date = bill.Date,
                Store = bill.Store,
                ImagePath = imagePath,
                CreatedAt = DateTime.UtcNow
            };
            _db.Bills.Add(billEntity);
            await _db.SaveChangesAsync(ct);

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

                await _repo.UpdateStockAsync(item.Id, parsedItem.Qty, EventSource.BillScan,
                    $"Purchased from {bill.Store}", ct);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
