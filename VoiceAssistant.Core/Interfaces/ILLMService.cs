namespace VoiceAssistant.Core.Interfaces;

public interface ILLMService
{
    Task<string> ChatAsync(string userMessage, CancellationToken ct = default);
}
