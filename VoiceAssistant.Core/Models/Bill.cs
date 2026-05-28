namespace VoiceAssistant.Core.Models;

public class Bill
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string Store { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string RawOcr { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BillItem> BillItems { get; set; } = new List<BillItem>();
}
