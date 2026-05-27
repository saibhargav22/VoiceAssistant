using System.Text;
using System.Text.Json;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Services;

public class TTSService : ITTSService
{
    private readonly HttpClient _http;

    public TTSService(HttpClient http)
    {
        _http = http;
    }

    public async Task<byte[]> SynthesiseAsync(string text, CancellationToken ct = default)
    {
        var payload = new { text, voice = "af_heart", speed = 1.0 };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("synthesise", content, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
