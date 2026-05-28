using System.Net.Http.Headers;
using System.Text.Json;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Services;

public class OCRService : IOCRService
{
    private readonly HttpClient _http;

    public OCRService(HttpClient http)
    {
        _http = http;
    }

    public async Task<OcrResult> ExtractTextAsync(byte[] imageData, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageData);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(imageContent, "image", "bill.jpg");

        var response = await _http.PostAsync("ocr", form, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        var fullText = doc.RootElement.GetProperty("full_text").GetString() ?? "";
        var overallConf = doc.RootElement.GetProperty("overall_confidence").GetSingle();

        var lines = doc.RootElement.GetProperty("lines").EnumerateArray()
            .Select(l => new OcrLine(
                l.GetProperty("text").GetString() ?? "",
                l.GetProperty("confidence").GetSingle()
            )).ToList();

        return new OcrResult(fullText, lines, overallConf);
    }
}
