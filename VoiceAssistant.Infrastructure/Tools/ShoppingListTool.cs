using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class ShoppingListTool : ITool
{
    private readonly IInventoryService _inventory;

    public ShoppingListTool(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    public string Name => "get_shopping_list";
    public string Description => "Get a shopping list of items that are low or out of stock. Use this when user says they are going shopping or asks what to buy.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        return await _inventory.GetShoppingListAsync(ct);
    }
}
