using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Core.Interfaces;

public interface IInventoryService
{
    Task<string> UpdateStockAsync(string itemName, decimal qtyChange, EventSource source, string note = "", CancellationToken ct = default);
    Task<List<Item>> GetLowStockItemsAsync(CancellationToken ct = default);
    Task<List<Item>> GetAllItemsAsync(CancellationToken ct = default);
    Task<string> GetShoppingListAsync(CancellationToken ct = default);
}
