using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetSpendByCategoryTool : ITool
{
    private readonly IFinancialService _finance;

    public GetSpendByCategoryTool(IFinancialService finance)
    {
        _finance = finance;
    }

    public string Name => "get_spend_by_category";
    public string Description => "Get spending breakdown by category for a month. Input format: YYYY-MM. If no date given use current month. Example: how much did I spend on groceries, show me spending by category.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var (year, month) = ParseYearMonth(input.Trim());
            return await _finance.GetSpendByCategoryAsync(year, month, ct);
        }
        catch
        {
            return "Could not get category spend. Please try again.";
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
