using Microsoft.AspNetCore.Mvc;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.API.Controllers;

[ApiController]
[Route("api/cupboards")]
public class CupboardController : ControllerBase
{
    private readonly IInventoryRepository _repo;

    public CupboardController(IInventoryRepository repo)
    {
        _repo = repo;
    }

    // GET /api/cupboards
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var cupboards = await _repo.GetAllCupboardsAsync(ct);
        return Ok(cupboards);
    }

    // POST /api/cupboards
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertCupboardRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest("Code is required.");

        var cupboard = await _repo.UpsertCupboardAsync(
            req.Code.Trim().ToUpper(),
            req.Name?.Trim() ?? "",
            req.Description?.Trim() ?? "",
            ct);

        return Ok(cupboard);
    }

    // GET /api/cupboards/{code}/items
    [HttpGet("{code}/items")]
    public async Task<IActionResult> GetItems(string code, CancellationToken ct)
    {
        var items = await _repo.GetItemsByCupboardAsync(code, ct);
        return Ok(items.Select(i => new
        {
            i.Id,
            i.Name,
            i.Unit,
            i.CupboardCode,
            i.SlotNumber,
            i.CategoryNumber,
            CurrentQty = i.Inventory?.CurrentQty ?? 0
        }));
    }

    // POST /api/cupboards/item-location
    [HttpPost("item-location")]
    public async Task<IActionResult> UpdateItemLocation([FromBody] UpdateLocationRequest req, CancellationToken ct)
    {
        await _repo.UpdateItemLocationAsync(req.ItemId, req.CupboardCode?.ToUpper(), req.SlotNumber, req.CategoryNumber, ct);
        return Ok(new { message = "Location updated." });
    }
}

[ApiController]
[Route("api/categories")]
public class CategoryController : ControllerBase
{
    private readonly IInventoryRepository _repo;

    public CategoryController(IInventoryRepository repo)
    {
        _repo = repo;
    }

    // GET /api/categories
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var cats = await _repo.GetAllCategoriesAsync(ct);
        return Ok(cats);
    }

    // POST /api/categories
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertCategoryRequest req, CancellationToken ct)
    {
        if (req.Number <= 0)
            return BadRequest("Number must be greater than 0.");

        var cat = await _repo.UpsertCategoryAsync(req.Number, req.Name?.Trim() ?? "", ct);
        return Ok(cat);
    }
}

public class UpsertCupboardRequest
{
    public string Code        { get; set; } = string.Empty;
    public string? Name       { get; set; }
    public string? Description { get; set; }
}

public class UpdateLocationRequest
{
    public int     ItemId         { get; set; }
    public string? CupboardCode   { get; set; }
    public int?    SlotNumber     { get; set; }
    public int?    CategoryNumber { get; set; }
}

public class UpsertCategoryRequest
{
    public int     Number { get; set; }
    public string? Name   { get; set; }
}
