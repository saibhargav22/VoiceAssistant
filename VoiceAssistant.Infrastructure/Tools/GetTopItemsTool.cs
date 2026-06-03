using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetTopItemsTool : ITool
{
    private readonly IFinancialService _finance;

    public GetTopItemsTool(IFinancialService finance)
    {
        _finance = finance;
    }

    public string Name => "get_top_items";
    public string Description => "Get the top items by spend for a month. Input format: YYYY-MM. If no date given use current month. Example: what do I spend most on, show me my top expenses this month.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var (year, month) = ParseYearMonth(input.Trim());
            return await _finance.GetTopItemsAsync(year, month, ct);
        }
        catch
        {
            return "Could not get top items. Please try again.";
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
