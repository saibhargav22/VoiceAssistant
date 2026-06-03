using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

public interface IFinancialRepository
{
    // Spend queries
    Task<decimal> GetTotalSpendAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<Dictionary<string, decimal>> GetSpendByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<Dictionary<string, decimal>> GetTopItemsAsync(DateTime from, DateTime to, int topN = 5, CancellationToken ct = default);
    Task<List<(DateTime Month, decimal Total)>> GetMonthlyTrendAsync(int months = 6, CancellationToken ct = default);

    // Budgets
    Task<List<Budget>> GetAllBudgetsAsync(CancellationToken ct = default);
    Task<Budget> UpsertBudgetAsync(string category, decimal monthlyLimit, CancellationToken ct = default);
    Task DeleteBudgetAsync(int id, CancellationToken ct = default);
}