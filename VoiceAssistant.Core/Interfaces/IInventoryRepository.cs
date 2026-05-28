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
}
