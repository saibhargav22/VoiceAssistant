namespace VoiceAssistant.Core.Models;

public class BillItem
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public float Confidence { get; set; }
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    public Bill Bill { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
