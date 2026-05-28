using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetInventoryTool : ITool
{
    private readonly IInventoryService _inventory;

    public GetInventoryTool(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    public string Name => "get_inventory";
    public string Description => "Get current stock levels of all grocery items. Use this when user asks what they have at home or wants to check stock.";

    private static string FormatQty(decimal qty)
    {
        return qty == Math.Floor(qty) ? ((int)qty).ToString() : qty.ToString("0.#");
    }

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var items = await _inventory.GetAllItemsAsync(ct);

        if (!items.Any())
            return "No items tracked yet. Scan a grocery bill to get started.";

        var lines = items.Select(x =>
        {
            var qty = x.Inventory?.CurrentQty ?? 0;
            var status = qty == 0 ? "out of stock" :
                         qty <= x.MinQty ? $"low, {FormatQty(qty)} {x.Unit} left" :
                         $"{FormatQty(qty)} {x.Unit}";
            return $"{x.Name}: {status}";
        });

        return "Current inventory: " + string.Join(", ", lines) + ".";
    }
}
