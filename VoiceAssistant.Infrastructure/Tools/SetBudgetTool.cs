using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class SetBudgetTool : ITool
{
    private readonly IFinancialService _finance;

    public SetBudgetTool(IFinancialService finance)
    {
        _finance = finance;
    }

    public string Name => "set_budget";
    public string Description => "Set a monthly budget for a category or total spending. Input format: category, amount. Example: set budget groceries 5000, set total budget 10000.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var parts = input.Split(',');
            if (parts.Length < 2)
                return "Please specify a category and amount. For example: groceries, 5000.";

            var category = parts[0].Trim().ToLower();
            if (!decimal.TryParse(parts[1].Trim(), out var limit) || limit <= 0)
                return "Please specify a valid amount. For example: groceries, 5000.";

            return await _finance.SetBudgetAsync(category, limit, ct);
        }
        catch
        {
            return "Could not set budget. Please say the category and amount clearly.";
        }
    }
}
