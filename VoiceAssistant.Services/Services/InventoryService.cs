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

    public async Task<string> UpdateStockAsync(string itemName, decimal qtyChange, EventSource source, string note = "", CancellationToken ct = default)
    {
        var item = await _repo.GetOrCreateItemAsync(itemName, "units", ct);
        await _repo.UpdateStockAsync(item.Id, qtyChange, source, note, ct);

        var inventory = item.Inventory;
        var newQty = inventory != null ? inventory.CurrentQty + qtyChange : qtyChange;
        newQty = Math.Max(0, newQty);

        var direction = qtyChange < 0 ? "decreased" : "increased";
        return $"{itemName} stock {direction}. Current quantity is approximately {newQty} {item.Unit}.";
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
            var status = qty == 0 ? "out of stock" : $"low — {qty} {x.Unit} left";
            return $"{x.Name} ({status})";
        });

        return "Shopping list: " + string.Join(", ", lines) + ".";
    }
}
