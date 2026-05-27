using System.Net.Http.Headers;
using System.Text.Json;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Services;

public class STTService : ISTTService
{
    private readonly HttpClient _http;

    public STTService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> TranscribeAsync(byte[] audioData, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(audioData);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent, "audio", "audio.wav");

        var response = await _http.PostAsync("transcribe", form, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }
}
