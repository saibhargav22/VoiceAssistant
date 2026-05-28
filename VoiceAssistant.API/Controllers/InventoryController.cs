using Microsoft.AspNetCore.Mvc;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;
    private readonly IInventoryRepository _repo;

    public InventoryController(IInventoryService inventory, IInventoryRepository repo)
    {
        _inventory = inventory;
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _inventory.GetAllItemsAsync();
        return Ok(items.Select(i => new
        {
            id = i.Id,
            name = i.Name,
            category = i.Category,
            unit = i.Unit,
            minQty = i.MinQty,
            currentQty = i.Inventory?.CurrentQty ?? 0,
            lastUpdated = i.Inventory?.LastUpdated
        }));
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] UpdateStockRequest request)
    {
        var items = await _repo.GetAllItemsAsync();
        var item = items.FirstOrDefault(i => i.Id == request.ItemId);
        if (item == null) return NotFound();

        await _repo.UpdateStockAsync(item.Id, request.Change, EventSource.ManualUI,
            "Manual update from UI");

        return Ok(new { message = "Stock updated." });
    }
}

public class UpdateStockRequest
{
    public int ItemId { get; set; }
    public decimal Change { get; set; }
}
