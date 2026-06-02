using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class UpdateLocationTool : ITool
{
    private readonly IInventoryRepository _repo;

    public UpdateLocationTool(IInventoryRepository repo)
    {
        _repo = repo;
    }

    public string Name => "update_location";
    public string Description => "Update the storage location of a grocery item. Input format: item_name, cupboard_code, slot_number. Example: rice, C1, 3. Slot number is optional.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var parts = input.Split(',');
            if (parts.Length < 2)
                return "Please specify item name and cupboard code. For example: rice, C1, 3.";

            var itemName     = parts[0].Trim();
            var cupboardCode = parts[1].Trim().ToUpper();
            int? slotNumber  = parts.Length > 2 && int.TryParse(parts[2].Trim(), out var slot)
                               ? slot : null;

            var item = await _repo.GetItemByNameAsync(itemName, ct);
            if (item == null)
                return $"I don't have {itemName} in your inventory.";

            await _repo.UpdateItemLocationAsync(item.Id, cupboardCode, slotNumber, item.CategoryNumber, ct);

            var location = $"cupboard {cupboardCode}";
            if (slotNumber.HasValue)
                location += $", slot {slotNumber}";

            return $"{item.Name} location updated to {location}.";
        }
        catch
        {
            return "Could not update location. Please say the item name, cupboard code, and slot number clearly.";
        }
    }
}
