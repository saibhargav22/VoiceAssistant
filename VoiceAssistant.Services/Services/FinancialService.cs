using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Services.Services;

public class FinancialService : IFinancialService
{
    private readonly IFinancialRepository _repo;

    public FinancialService(IFinancialRepository repo)
    {
        _repo = repo;
    }

    private static (DateTime From, DateTime To) MonthRange(int year, int month)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1).AddTicks(-1);
        return (from, to);
    }

    private static string FormatAmount(decimal amount) => $"₹{amount:N0}";

    public async Task<string> GetMonthlySpendAsync(int year, int month, CancellationToken ct = default)
    {
        var (from, to) = MonthRange(year, month);
        var total = await _repo.GetTotalSpendAsync(from, to, ct);
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");

        if (total == 0)
            return $"No spending recorded for {monthName}.";

        return $"You spent {FormatAmount(total)} in {monthName}.";
    }

    public async Task<string> GetSpendByCategoryAsync(int year, int month, CancellationToken ct = default)
    {
        var (from, to) = MonthRange(year, month);
        var breakdown = await _repo.GetSpendByCategoryAsync(from, to, ct);
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");

        if (!breakdown.Any())
            return $"No spending data found for {monthName}.";

        var lines = breakdown
            .OrderByDescending(x => x.Value)
            .Select(x => $"{x.Key}: {FormatAmount(x.Value)}");

        return $"Spending by category for {monthName}. {string.Join(". ", lines)}.";
    }

    public async Task<string> GetSpendTrendAsync(CancellationToken ct = default)
    {
        var trend = await _repo.GetMonthlyTrendAsync(3, ct);

        if (trend.Count < 2)
            return "Not enough data to show a trend yet. Need at least 2 months of bills.";

        var current  = trend.Last();
        var previous = trend[trend.Count - 2];

        var diff = current.Total - previous.Total;
        var pct  = previous.Total > 0
            ? Math.Abs(Math.Round(diff / previous.Total * 100, 0))
            : 0;

        var direction = diff > 0 ? "more" : "less";
        var currentMonth  = current.Month.ToString("MMMM");
        var previousMonth = previous.Month.ToString("MMMM");

        if (diff == 0)
            return $"Your spending in {currentMonth} is the same as {previousMonth} at {FormatAmount(current.Total)}.";

        return $"You spent {FormatAmount(Math.Abs(diff))} {direction} in {currentMonth} compared to {previousMonth}. " +
               $"That is a {pct}% {(diff > 0 ? "increase" : "decrease")}.";
    }

    public async Task<string> GetTopItemsAsync(int year, int month, CancellationToken ct = default)
    {
        var (from, to) = MonthRange(year, month);
        var top = await _repo.GetTopItemsAsync(from, to, 5, ct);
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");

        if (!top.Any())
            return $"No spending data found for {monthName}.";

        var lines = top
            .OrderByDescending(x => x.Value)
            .Select((x, i) => $"{i + 1}. {x.Key}: {FormatAmount(x.Value)}");

        return $"Top items by spend in {monthName}. {string.Join(". ", lines)}.";
    }

    public async Task<string> GetBudgetStatusAsync(CancellationToken ct = default)
    {
        var now    = DateTime.UtcNow;
        var (from, to) = MonthRange(now.Year, now.Month);
        var budgets    = await _repo.GetAllBudgetsAsync(ct);

        if (!budgets.Any())
            return "No budgets set. You can set a budget by saying set budget groceries 5000.";

        var totalSpend    = await _repo.GetTotalSpendAsync(from, to, ct);
        var categorySpend = await _repo.GetSpendByCategoryAsync(from, to, ct);

        var lines = new List<string>();

        foreach (var budget in budgets)
        {
            if (budget.Category == "total")
            {
                var status = totalSpend > budget.MonthlyLimit ? "over" : "within";
                lines.Add($"Total budget {FormatAmount(budget.MonthlyLimit)}: spent {FormatAmount(totalSpend)}, {status} budget");
            }
            else
            {
                var spent = categorySpend.TryGetValue(budget.Category, out var s) ? s : 0;
                var status = spent > budget.MonthlyLimit ? "over" : "within";
                lines.Add($"{budget.Category} budget {FormatAmount(budget.MonthlyLimit)}: spent {FormatAmount(spent)}, {status} budget");
            }
        }

        return string.Join(". ", lines) + ".";
    }

    public async Task<string> SetBudgetAsync(string category, decimal limit, CancellationToken ct = default)
    {
        await _repo.UpsertBudgetAsync(category.ToLower(), limit, ct);
        return $"Budget for {category} set to {FormatAmount(limit)} per month.";
    }

    public async Task<object> GetDashboardDataAsync(int year, int month, CancellationToken ct = default)
    {
        var (from, to) = MonthRange(year, month);

        var total      = await _repo.GetTotalSpendAsync(from, to, ct);
        var categories = await _repo.GetSpendByCategoryAsync(from, to, ct);
        var topItems   = await _repo.GetTopItemsAsync(from, to, 5, ct);
        var trend      = await _repo.GetMonthlyTrendAsync(6, ct);
        var budgets    = await _repo.GetAllBudgetsAsync(ct);

        // Previous month for comparison
        var prevMonth  = from.AddMonths(-1);
        var (pFrom, pTo) = MonthRange(prevMonth.Year, prevMonth.Month);
        var prevTotal  = await _repo.GetTotalSpendAsync(pFrom, pTo, ct);

        return new
        {
            month = new DateTime(year, month, 1).ToString("MMMM yyyy"),
            total,
            prevTotal,
            changePercent = prevTotal > 0
                ? Math.Round((total - prevTotal) / prevTotal * 100, 1)
                : (decimal?)null,
            categories = categories.OrderByDescending(x => x.Value)
                .Select(x => new { name = x.Key, amount = x.Value }),
            topItems = topItems.OrderByDescending(x => x.Value)
                .Select(x => new { name = x.Key, amount = x.Value }),
            trend = trend.Select(t => new
            {
                month = t.Month.ToString("MMM yyyy"),
                total = t.Total
            }),
            budgets = budgets.Select(b =>
            {
                var spent = b.Category == "total"
                    ? total
                    : categories.TryGetValue(b.Category, out var s) ? s : 0;
                return new
                {
                    b.Id,
                    b.Category,
                    b.MonthlyLimit,
                    spent,
                    overBudget = spent > b.MonthlyLimit
                };
            })
        };
    }
}