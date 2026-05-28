namespace VoiceAssistant.Core.Models;

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "grocery";
    public string Unit { get; set; } = "units";
    public decimal MinQty { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Inventory? Inventory { get; set; }
    public ICollection<BillItem> BillItems { get; set; } = new List<BillItem>();
    public ICollection<StockEvent> StockEvents { get; set; } = new List<StockEvent>();
}
