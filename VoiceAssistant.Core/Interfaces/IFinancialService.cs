using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

public interface IFinancialService
{
    Task<string> GetMonthlySpendAsync(int year, int month, CancellationToken ct = default);
    Task<string> GetSpendByCategoryAsync(int year, int month, CancellationToken ct = default);
    Task<string> GetSpendTrendAsync(CancellationToken ct = default);
    Task<string> GetTopItemsAsync(int year, int month, CancellationToken ct = default);
    Task<string> GetBudgetStatusAsync(CancellationToken ct = default);
    Task<string> SetBudgetAsync(string category, decimal limit, CancellationToken ct = default);

    // For UI
    Task<object> GetDashboardDataAsync(int year, int month, CancellationToken ct = default);
}