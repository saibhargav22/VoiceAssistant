using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;
using System.Text.RegularExpressions;

namespace VoiceAssistant.Services.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repo;

    public InventoryService(IInventoryRepository repo)
    {
        _repo = repo;
    }

    internal static string NormaliseName(string name)
    {
        // lowercase, trim whitespace
        name = name.Trim().ToLowerInvariant();
        // remove size/weight tokens like "1kg", "500g", "2l", "1ltr"
        name = Regex.Replace(name, @"\b\d+(\.\d+)?\s*(kg|g|gm|gms|l|ltr|litre|ml|units?)\b", "");
        // remove common noise words
        name = Regex.Replace(name, @"\b(pack|packet|pouch|bag|box|bottle|tin|jar|pkt)\b", "");
        // collapse multiple spaces
        name = Regex.Replace(name, @"\s{2,}", " ").Trim();
        return name;
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
        var normalised = NormaliseName(itemName);
        var item = await _repo.GetOrCreateItemAsync(normalised, "units", ct);
        await _repo.UpdateStockAsync(item.Id, qtyChange, source, note, ct);

        // Reload fresh qty
        var updated = await _repo.GetItemByNameAsync(normalised, ct);
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
