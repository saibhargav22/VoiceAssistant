using System.Text.Json;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Services;

public class SearXNGService : IWebSearchService
{
    private readonly HttpClient _http;

    public SearXNGService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> SearchAsync(string query, int maxResults = 3, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(query);
        var response = await _http.GetAsync(
            $"search?q={encoded}&format=json&language=en", ct);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc  = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("results", out var results))
            return string.Empty;

        var snippets = results.EnumerateArray()
            .Take(maxResults)
            .Where(r => r.TryGetProperty("content", out _))
            .Select(r =>
            {
                var title   = r.TryGetProperty("title",   out var t) ? t.GetString() : "";
                var content = r.TryGetProperty("content", out var c) ? c.GetString() : "";
                var url     = r.TryGetProperty("url",     out var u) ? u.GetString() : "";
                return $"Source: {title}\n{content}\n({url})";
            });

        return string.Join("\n\n", snippets);
    }
}