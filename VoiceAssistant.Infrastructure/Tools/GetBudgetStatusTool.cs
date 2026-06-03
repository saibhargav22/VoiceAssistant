using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetBudgetStatusTool : ITool
{
    private readonly IFinancialService _finance;

    public GetBudgetStatusTool(IFinancialService finance)
    {
        _finance = finance;
    }

    public string Name => "get_budget_status";
    public string Description => "Check if spending is within budget this month. Use when user asks about budget. Example: am I over budget, how is my budget looking, budget status.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            return await _finance.GetBudgetStatusAsync(ct);
        }
        catch
        {
            return "Could not get budget status. Please try again.";
        }
    }
}
