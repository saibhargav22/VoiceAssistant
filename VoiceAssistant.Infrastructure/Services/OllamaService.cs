using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Services;

public class OllamaService : ILLMService
{
    private readonly HttpClient _http;
    private readonly string _model;

    private const string DefaultSystem =
        "You are a helpful voice assistant. Always reply in plain conversational sentences. " +
        "Never use markdown, bullet points, asterisks, headers, or special formatting. " +
        "Keep answers concise and natural sounding.";

    public OllamaService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _model = config["Ollama:Model"] ?? "gemma3:4b";
    }

    public async Task<string> ChatAsync(string userMessage, string? systemPrompt = null, bool jsonMode = false, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"]  = _model,
            ["system"] = systemPrompt ?? DefaultSystem,
            ["prompt"] = userMessage,
            ["stream"] = false,
        };
        if (jsonMode) payload["format"] = "json";

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("api/generate", content, ct);
        response.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string userMessage,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model  = _model,
            system = systemPrompt ?? DefaultSystem,
            prompt = userMessage,
            stream = true,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); }
            catch (JsonException) { continue; }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("response", out var tokenProp))
                {
                    var token = tokenProp.GetString() ?? string.Empty;
                    if (token.Length > 0) yield return token;
                }

                if (doc.RootElement.TryGetProperty("done", out var done) && done.GetBoolean())
                    yield break;
            }
        }
    }
}

