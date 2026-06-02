using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetCupboardContentsTool : ITool
{
    private readonly IInventoryRepository _repo;

    public GetCupboardContentsTool(IInventoryRepository repo)
    {
        _repo = repo;
    }

    public string Name => "get_cupboard_contents";
    public string Description => "List all items stored in a specific cupboard. Use when user asks what is in a cupboard. Input is the cupboard code. Example: C1, C2.";

    private static string FormatQty(decimal qty)
    {
        return qty == Math.Floor(qty) ? ((int)qty).ToString() : qty.ToString("0.#");
    }

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var cupboardCode = input.Trim().ToUpper();
            var items = await _repo.GetItemsByCupboardAsync(cupboardCode, ct);

            if (!items.Any())
                return $"No items are assigned to cupboard {cupboardCode}.";

            var lines = items.Select(x =>
            {
                var qty = x.Inventory?.CurrentQty ?? 0;
                var slot = x.SlotNumber.HasValue ? $" (slot {x.SlotNumber})" : "";
                var status = qty == 0 ? "out of stock" : $"{FormatQty(qty)} {x.Unit}";
                return $"{x.Name}{slot}: {status}";
            });

            return $"Cupboard {cupboardCode} contains: {string.Join(", ", lines)}.";
        }
        catch
        {
            return "Could not get cupboard contents. Please say the cupboard code clearly.";
        }
    }
}
