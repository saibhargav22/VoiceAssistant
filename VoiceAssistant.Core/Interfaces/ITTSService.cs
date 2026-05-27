namespace VoiceAssistant.Core.Interfaces;

public interface ITTSService
{
    Task<byte[]> SynthesiseAsync(string text, CancellationToken ct = default);
}
