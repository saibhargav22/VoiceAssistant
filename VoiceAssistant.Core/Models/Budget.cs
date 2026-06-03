namespace VoiceAssistant.Core.Models;

public class Budget
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;  // e.g. "groceries", "total"
    public decimal MonthlyLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}