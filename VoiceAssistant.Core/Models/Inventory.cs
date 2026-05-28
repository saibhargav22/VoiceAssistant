namespace VoiceAssistant.Core.Models;

public class Inventory
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public decimal CurrentQty { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public Item Item { get; set; } = null!;
}
