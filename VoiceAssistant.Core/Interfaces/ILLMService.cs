namespace VoiceAssistant.Core.Interfaces;

public interface ILLMService
{
    Task<string> ChatAsync(string userMessage, string? systemPrompt = null, bool jsonMode = false, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string userMessage, string? systemPrompt = null, CancellationToken ct = default);
}
