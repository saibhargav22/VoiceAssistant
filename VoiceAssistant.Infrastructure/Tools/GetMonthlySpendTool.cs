using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetMonthlySpendTool : ITool
{
    private readonly IFinancialService _finance;

    public GetMonthlySpendTool(IFinancialService finance)
    {
        _finance = finance;
    }

    public string Name => "get_monthly_spend";
    public string Description => "Get total spending for a specific month. Input format: YYYY-MM. If no date given use current month. Example: how much did I spend this month, how much did I spend in April.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var (year, month) = ParseYearMonth(input.Trim());
            return await _finance.GetMonthlySpendAsync(year, month, ct);
        }
        catch
        {
            return "Could not get monthly spend. Please try again.";
        }
    }

    private static (int Year, int Month) ParseYearMonth(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (DateTime.UtcNow.Year, DateTime.UtcNow.Month);

        if (DateTime.TryParseExact(input, "yyyy-MM",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d))
            return (d.Year, d.Month);

        return (DateTime.UtcNow.Year, DateTime.UtcNow.Month);
    }
}
