using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetSpendTrendTool : ITool
{
    private readonly IFinancialService _finance;

    public GetSpendTrendTool(IFinancialService finance)
    {
        _finance = finance;
    }

    public string Name => "get_spend_trend";
    public string Description => "Compare spending between months to show a trend. Use when user asks if they are spending more or less than before. Example: am I spending more than last month, how is my spending trending.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            return await _finance.GetSpendTrendAsync(ct);
        }
        catch
        {
            return "Could not get spending trend. Please try again.";
        }
    }
}
