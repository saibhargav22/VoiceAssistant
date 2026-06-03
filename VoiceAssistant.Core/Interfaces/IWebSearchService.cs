namespace VoiceAssistant.Core.Interfaces;

public interface IWebSearchService
{
    Task<string> SearchAsync(string query, int maxResults = 3, CancellationToken ct = default);
}