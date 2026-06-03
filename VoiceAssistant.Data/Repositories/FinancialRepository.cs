using Microsoft.EntityFrameworkCore;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Data.Repositories;

public class FinancialRepository : IFinancialRepository
{
    private readonly AppDbContext _db;

    public FinancialRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetTotalSpendAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _db.BillItems
            .Include(x => x.Bill)
            .Where(x => x.Bill.Date >= from && x.Bill.Date <= to)
            .SumAsync(x => x.Price * x.Qty, ct);
    }

    public async Task<Dictionary<string, decimal>> GetSpendByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var rows = await _db.BillItems
            .Include(x => x.Bill)
            .Include(x => x.Item)
            .Where(x => x.Bill.Date >= from && x.Bill.Date <= to)
            .GroupBy(x => x.Item.Category ?? "uncategorised")
            .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Price * x.Qty) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Category, r => r.Total);
    }

    public async Task<Dictionary<string, decimal>> GetTopItemsAsync(DateTime from, DateTime to, int topN = 5, CancellationToken ct = default)
    {
        var rows = await _db.BillItems
            .Include(x => x.Bill)
            .Include(x => x.Item)
            .Where(x => x.Bill.Date >= from && x.Bill.Date <= to)
            .GroupBy(x => x.Item.Name)
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Price * x.Qty) })
            .OrderByDescending(x => x.Total)
            .Take(topN)
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Name, r => r.Total);
    }

    public async Task<List<(DateTime Month, decimal Total)>> GetMonthlyTrendAsync(int months = 6, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.AddMonths(-months + 1);
        var start = new DateTime(from.Year, from.Month, 1);

        var rows = await _db.BillItems
            .Include(x => x.Bill)
            .Where(x => x.Bill.Date >= start)
            .GroupBy(x => new { x.Bill.Date.Year, x.Bill.Date.Month })
            .Select(g => new
            {
                Year  = g.Key.Year,
                Month = g.Key.Month,
                Total = g.Sum(x => x.Price * x.Qty)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        return rows.Select(r => (new DateTime(r.Year, r.Month, 1), r.Total)).ToList();
    }

    public async Task<List<Budget>> GetAllBudgetsAsync(CancellationToken ct = default)
    {
        return await _db.Budgets
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ToListAsync(ct);
    }

    public async Task<Budget> UpsertBudgetAsync(string category, decimal monthlyLimit, CancellationToken ct = default)
    {
        var budget = await _db.Budgets
            .FirstOrDefaultAsync(x => x.Category == category.ToLower(), ct);

        if (budget == null)
        {
            budget = new Budget { Category = category.ToLower(), CreatedAt = DateTime.UtcNow };
            _db.Budgets.Add(budget);
        }

        budget.MonthlyLimit = monthlyLimit;
        budget.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return budget;
    }

    public async Task DeleteBudgetAsync(int id, CancellationToken ct = default)
    {
        var budget = await _db.Budgets.FindAsync(new object[] { id }, ct);
        if (budget != null)
        {
            _db.Budgets.Remove(budget);
            await _db.SaveChangesAsync(ct);
        }
    }
}