namespace VoiceAssistant.Core.Interfaces;

public interface ILLMService
{
    Task<string> ChatAsync(string userMessage, string? systemPrompt = null, CancellationToken ct = default);
    Task<string> ChatWithImageAsync(string prompt, byte[] imageData, CancellationToken ct = default);
}