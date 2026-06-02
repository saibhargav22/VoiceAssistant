using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

public interface IInventoryRepository
{
    Task<Item?> GetItemByNameAsync(string name, CancellationToken ct = default);
    Task<Item> GetOrCreateItemAsync(string name, string unit, CancellationToken ct = default);
    Task<List<Item>> GetLowStockItemsAsync(CancellationToken ct = default);
    Task<List<Item>> GetAllItemsAsync(CancellationToken ct = default);
    Task UpdateStockAsync(int itemId, decimal qtyChange, EventSource source, string note, CancellationToken ct = default);
    Task<List<StockEvent>> GetItemHistoryAsync(int itemId, CancellationToken ct = default);

    // Location
    Task<List<Item>> GetItemsByCupboardAsync(string cupboardCode, CancellationToken ct = default);
    Task<List<Item>> GetItemsByCategoryNumberAsync(int categoryNumber, CancellationToken ct = default);
    Task UpdateItemLocationAsync(int itemId, string? cupboardCode, int? slotNumber, int? categoryNumber, CancellationToken ct = default);

    // Cupboards
    Task<List<Cupboard>> GetAllCupboardsAsync(CancellationToken ct = default);
    Task<Cupboard> UpsertCupboardAsync(string code, string name, string description, CancellationToken ct = default);

    // Categories
    Task<List<StorageCategory>> GetAllCategoriesAsync(CancellationToken ct = default);
    Task<StorageCategory> UpsertCategoryAsync(int number, string name, CancellationToken ct = default);
}
