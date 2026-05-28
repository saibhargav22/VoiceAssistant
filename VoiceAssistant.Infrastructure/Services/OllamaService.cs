using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Services;

public class OllamaService : ILLMService
{
    private readonly HttpClient _http;
    private readonly string _model;

    private const string SystemPrompt =
        "You are a helpful voice assistant. " +
        "Always reply in plain conversational sentences. " +
        "Never use markdown, bullet points, asterisks, headers, or special formatting. " +
        "Keep answers concise and natural sounding.";

    public OllamaService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _model = config["Ollama:Model"] ?? "gemma3:4b";
    }

    public async Task<string> ChatAsync(string userMessage, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            system = SystemPrompt,
            prompt = userMessage,
            stream = false
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("api/generate", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }
}
