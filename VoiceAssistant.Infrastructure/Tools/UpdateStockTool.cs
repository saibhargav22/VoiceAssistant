using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Infrastructure.Tools;

public class UpdateStockTool : ITool
{
    private readonly IInventoryService _inventory;

    public UpdateStockTool(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    public string Name => "update_stock";
    public string Description => "Update the stock quantity of a grocery item. Use negative qty for consumption (finished/used), positive for restocking. Example: item=rice, qty=-1 means rice is finished.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var parts = input.Split(',');
            var itemName = parts[0].Trim();
            var qty = decimal.Parse(parts[1].Trim());
            var note = parts.Length > 2 ? parts[2].Trim() : "voice command";

            return await _inventory.UpdateStockAsync(itemName, qty, EventSource.VoiceCommand, note, ct);
        }
        catch
        {
            return "Could not update stock. Please say the item name and quantity clearly.";
        }
    }
}
