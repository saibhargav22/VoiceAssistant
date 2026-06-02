namespace VoiceAssistant.Core.Models;

public class Cupboard
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;   // e.g. "C1"
    public string Name { get; set; } = string.Empty;   // e.g. "Kitchen top shelf"
    public string Description { get; set; } = string.Empty;
}