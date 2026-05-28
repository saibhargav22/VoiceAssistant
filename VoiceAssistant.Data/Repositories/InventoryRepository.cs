using Microsoft.EntityFrameworkCore;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Data.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _db;

    public InventoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Item?> GetItemByNameAsync(string name, CancellationToken ct = default)
    {
        return await _db.Items
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), ct);
    }

    public async Task<Item> GetOrCreateItemAsync(string name, string unit, CancellationToken ct = default)
    {
        var item = await GetItemByNameAsync(name, ct);
        if (item != null) return item;

        item = new Item { Name = name, Unit = unit };
        _db.Items.Add(item);

        var inventory = new Inventory { Item = item, CurrentQty = 0 };
        _db.Inventories.Add(inventory);

        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<List<Item>> GetLowStockItemsAsync(CancellationToken ct = default)
    {
        return await _db.Items
            .Include(x => x.Inventory)
            .Where(x => x.Inventory != null && x.Inventory.CurrentQty <= x.MinQty)
            .ToListAsync(ct);
    }

    public async Task<List<Item>> GetAllItemsAsync(CancellationToken ct = default)
    {
        return await _db.Items
            .Include(x => x.Inventory)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task UpdateStockAsync(int itemId, decimal qtyChange, EventSource source, string note, CancellationToken ct = default)
    {
        var inventory = await _db.Inventories.FirstOrDefaultAsync(x => x.ItemId == itemId, ct);
        if (inventory == null)
        {
            inventory = new Inventory { ItemId = itemId, CurrentQty = 0 };
            _db.Inventories.Add(inventory);
        }

        inventory.CurrentQty = Math.Max(0, inventory.CurrentQty + qtyChange);
        inventory.LastUpdated = DateTime.UtcNow;

        var stockEvent = new StockEvent
        {
            ItemId = itemId,
            EventType = source == EventSource.VoiceCommand ? EventType.VoiceCommand :
                        source == EventSource.BillScan ? EventType.BillScan : EventType.ManualUI,
            QtyChange = qtyChange,
            Source = source,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };
        _db.StockEvents.Add(stockEvent);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<StockEvent>> GetItemHistoryAsync(int itemId, CancellationToken ct = default)
    {
        return await _db.StockEvents
            .Where(x => x.ItemId == itemId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
    }
}
