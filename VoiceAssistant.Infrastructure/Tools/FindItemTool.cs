using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class FindItemTool : ITool
{
    private readonly IInventoryRepository _repo;

    public FindItemTool(IInventoryRepository repo)
    {
        _repo = repo;
    }

    public string Name => "find_item";
    public string Description => "Find where a grocery item is stored. Use when user asks where something is. Example: where is the rice, where do I keep the salt.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var itemName = input.Trim();
            var item = await _repo.GetItemByNameAsync(itemName, ct);

            if (item == null)
                return $"I don't have {itemName} in your inventory.";

            if (item.CupboardCode == null)
                return $"{item.Name} is in your inventory but no storage location has been set for it.";

            var location = $"cupboard {item.CupboardCode}";
            if (item.SlotNumber.HasValue)
                location += $", slot {item.SlotNumber}";

            return $"{item.Name} is stored in {location}.";
        }
        catch
        {
            return "Could not find item location. Please say the item name clearly.";
        }
    }
}
