namespace VoiceAssistant.Core.Interfaces;

public interface ITTSService
{
    Task<byte[]> SynthesiseAsync(string text, double speed = 1.0, CancellationToken ct = default);
}
