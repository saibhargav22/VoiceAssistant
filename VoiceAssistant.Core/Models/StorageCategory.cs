namespace VoiceAssistant.Core.Models;

public class StorageCategory
{
    public int Id { get; set; }
    public int Number { get; set; }         // e.g. 1
    public string Name { get; set; } = string.Empty;  // e.g. "Grains & Pulses"
}