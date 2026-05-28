namespace VoiceAssistant.Core.Interfaces;

public record OcrLine(string Text, float Confidence);
public record OcrResult(string FullText, List<OcrLine> Lines, float OverallConfidence);

public interface IOCRService
{
    Task<OcrResult> ExtractTextAsync(byte[] imageData, CancellationToken ct = default);
}
