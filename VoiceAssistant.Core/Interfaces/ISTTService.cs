namespace VoiceAssistant.Core.Interfaces;

public interface ISTTService
{
    Task<string> TranscribeAsync(byte[] audioData, CancellationToken ct = default);
}
