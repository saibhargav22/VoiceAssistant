using Microsoft.AspNetCore.Mvc;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.API.Controllers;

[ApiController]
[Route("api/finance")]
public class FinanceController : ControllerBase
{
    private readonly IFinancialService _finance;
    private readonly IFinancialRepository _repo;

    public FinanceController(IFinancialService finance, IFinancialRepository repo)
    {
        _finance = finance;
        _repo = repo;
    }

    // GET /api/finance/dashboard?year=2025&month=6
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        var y = year  ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;

        if (m < 1 || m > 12) return BadRequest("Invalid month.");
        if (y < 2000 || y > 2100) return BadRequest("Invalid year.");

        var data = await _finance.GetDashboardDataAsync(y, m, ct);
        return Ok(data);
    }

    // GET /api/finance/trend?months=6
    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend(
        [FromQuery] int months = 6,
        CancellationToken ct = default)
    {
        var trend = await _repo.GetMonthlyTrendAsync(months, ct);
        return Ok(trend.Select(t => new
        {
            month = t.Month.ToString("MMM yyyy"),
            total = t.Total
        }));
    }

    // GET /api/finance/budgets
    [HttpGet("budgets")]
    public async Task<IActionResult> GetBudgets(CancellationToken ct)
    {
        var budgets = await _repo.GetAllBudgetsAsync(ct);
        return Ok(budgets);
    }

    // POST /api/finance/budgets
    [HttpPost("budgets")]
    public async Task<IActionResult> UpsertBudget(
        [FromBody] UpsertBudgetRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Category))
            return BadRequest("Category is required.");
        if (req.MonthlyLimit <= 0)
            return BadRequest("Monthly limit must be greater than 0.");

        var budget = await _repo.UpsertBudgetAsync(req.Category.ToLower(), req.MonthlyLimit, ct);
        return Ok(budget);
    }

    // DELETE /api/finance/budgets/{id}
    [HttpDelete("budgets/{id}")]
    public async Task<IActionResult> DeleteBudget(int id, CancellationToken ct)
    {
        await _repo.DeleteBudgetAsync(id, ct);
        return Ok(new { message = "Budget deleted." });
    }
}

public class UpsertBudgetRequest
{
    public string  Category     { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
}
