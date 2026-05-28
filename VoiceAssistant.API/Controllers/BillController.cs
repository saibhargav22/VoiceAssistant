using Microsoft.AspNetCore.Mvc;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillController : ControllerBase
{
    private readonly IBillService _billService;

    public BillController(IBillService billService)
    {
        _billService = billService;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(IFormFile image)
    {
        if (image == null || image.Length == 0)
            return BadRequest(new { error = "no image provided" });

        // Save image
        var uploadsDir = "/home/viddharth/VoiceAssistant/data/bills";
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{image.FileName}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var imageData = ms.ToArray();

        await System.IO.File.WriteAllBytesAsync(filePath, imageData);

        // Scan and parse
        var parsedBill = await _billService.ScanBillAsync(imageData);

        // Return parsed result for user review before saving
        return Ok(new
        {
            store = parsedBill.Store,
            date = parsedBill.Date,
            items = parsedBill.Items.Select(i => new
            {
                name = i.Name,
                qty = i.Qty,
                unit = i.Unit,
                price = i.Price,
                confidence = i.Confidence
            }),
            imagePath = filePath
        });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmBillRequest request)
    {
        var bill = new Core.Interfaces.ParsedBill(
            request.Store,
            request.Date,
            request.Items.Select(i => new Core.Interfaces.ParsedBillItem(
                i.Name, i.Unit, i.Qty, i.Price, i.Confidence
            )).ToList()
        );

        await _billService.SaveBillAsync(bill, request.ImagePath);
        return Ok(new { message = $"Bill saved. {request.Items.Count} items added to inventory." });
    }
}

public class ConfirmBillRequest
{
    public string Store { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public List<ConfirmBillItem> Items { get; set; } = new();
}

public class ConfirmBillItem
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public float Confidence { get; set; }
}
