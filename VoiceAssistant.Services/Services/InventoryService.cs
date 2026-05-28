using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Services.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repo;

    public InventoryService(IInventoryRepository repo)
    {
        _repo = repo;
    }

    private static string FormatQty(decimal qty)
    {
        // 1.0 → "1", 1.5 → "1.5"
        return qty == Math.Floor(qty)
            ? ((int)qty).ToString()
            : qty.ToString("0.#");
    }

    private static string Pluralise(string unit, decimal qty)
    {
        // avoid "1 units" — use "1 unit"
        if (qty == 1 && unit.EndsWith("s") && unit != "units")
            return unit.TrimEnd('s');
        if (qty == 1 && unit == "units") return "unit";
        return unit;
    }

    public async Task<string> UpdateStockAsync(string itemName, decimal qtyChange, EventSource source, string note = "", CancellationToken ct = default)
    {
        var item = await _repo.GetOrCreateItemAsync(itemName, "units", ct);
        await _repo.UpdateStockAsync(item.Id, qtyChange, source, note, ct);

        // Reload fresh qty
        var updated = await _repo.GetItemByNameAsync(itemName, ct);
        var newQty = updated?.Inventory?.CurrentQty ?? 0;

        var direction = qtyChange < 0 ? "decreased" : "increased";
        var unit = Pluralise(item.Unit, newQty);
        return $"{itemName} stock {direction}. You have {FormatQty(newQty)} {unit} left.";
    }

    public async Task<List<Item>> GetLowStockItemsAsync(CancellationToken ct = default)
    {
        return await _repo.GetLowStockItemsAsync(ct);
    }

    public async Task<List<Item>> GetAllItemsAsync(CancellationToken ct = default)
    {
        return await _repo.GetAllItemsAsync(ct);
    }

    public async Task<string> GetShoppingListAsync(CancellationToken ct = default)
    {
        var lowItems = await _repo.GetLowStockItemsAsync(ct);

        if (!lowItems.Any())
            return "Your stock looks good. Nothing urgently needed right now.";

        var lines = lowItems.Select(x =>
        {
            var qty = x.Inventory?.CurrentQty ?? 0;
            var unit = Pluralise(x.Unit, qty);
            var status = qty == 0
                ? "out of stock"
                : $"only {FormatQty(qty)} {unit} left";
            return $"{x.Name}, {status}";
        });

        return "Here is your shopping list. " + string.Join(". ", lines) + ".";
    }
}
