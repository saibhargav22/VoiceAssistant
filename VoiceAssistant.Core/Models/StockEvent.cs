namespace VoiceAssistant.Core.Models;

public enum EventType
{
    BillScan,
    VoiceCommand,
    ManualUI
}

public enum EventSource
{
    BillScan,
    VoiceCommand,
    ManualUI
}

public class StockEvent
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public EventType EventType { get; set; }
    public decimal QtyChange { get; set; }
    public EventSource Source { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Item Item { get; set; } = null!;
}
