using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class GetCategoryItemsTool : ITool
{
    private readonly IInventoryRepository _repo;

    public GetCategoryItemsTool(IInventoryRepository repo)
    {
        _repo = repo;
    }

    public string Name => "get_category_items";
    public string Description => "List all items in a storage category by category number. Use when user asks about a category of items. Example: show me category 1, what grains do I have.";

    private static string FormatQty(decimal qty)
    {
        return qty == Math.Floor(qty) ? ((int)qty).ToString() : qty.ToString("0.#");
    }

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            if (!int.TryParse(input.Trim(), out var categoryNumber))
                return "Please provide a category number. For example: category 1.";

            var items = await _repo.GetItemsByCategoryNumberAsync(categoryNumber, ct);

            if (!items.Any())
                return $"No items are assigned to category {categoryNumber}.";

            var lines = items.Select(x =>
            {
                var qty = x.Inventory?.CurrentQty ?? 0;
                var status = qty == 0 ? "out of stock" : $"{FormatQty(qty)} {x.Unit}";
                return $"{x.Name}: {status}";
            });

            return $"Category {categoryNumber} items: {string.Join(", ", lines)}.";
        }
        catch
        {
            return "Could not get category items. Please provide a valid category number.";
        }
    }
}
