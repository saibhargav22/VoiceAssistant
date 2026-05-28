using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.API.Hubs;

public class VoiceHub : Hub
{
    private readonly ISTTService _stt;
    private readonly ILLMService _llm;
    private readonly ITTSService _tts;

    public VoiceHub(ISTTService stt, ILLMService llm, ITTSService tts)
    {
        _stt = stt;
        _llm = llm;
        _tts = tts;
    }

    private static string StripMarkdown(string text)
    {
        // Remove bold/italic markers
        text = Regex.Replace(text, @"\*{1,3}(.+?)\*{1,3}", "$1");
        // Remove headers
        text = Regex.Replace(text, @"^#{1,6}\s+", "", RegexOptions.Multiline);
        // Remove bullet points and dashes
        text = Regex.Replace(text, @"^\s*[\*\-•]\s+", "", RegexOptions.Multiline);
        // Remove numbered lists
        text = Regex.Replace(text, @"^\s*\d+\.\s+", "", RegexOptions.Multiline);
        // Remove code blocks
        text = Regex.Replace(text, @"```[\s\S]*?```", "");
        text = Regex.Replace(text, @"`(.+?)`", "$1");
        // Remove links
        text = Regex.Replace(text, @"\[(.+?)\]\(.+?\)", "$1");
        // Collapse multiple newlines into one
        text = Regex.Replace(text, @"\n{2,}", " ");
        text = text.Replace("\n", " ").Trim();
        return text;
    }

    public async Task ProcessAudio(string audioBase64)
    {
        try
        {
            var audioData = Convert.FromBase64String(audioBase64);

            await Clients.Caller.SendAsync("OnStatus", "Transcribing...");
            var question = await _stt.TranscribeAsync(audioData);

            if (string.IsNullOrWhiteSpace(question))
            {
                await Clients.Caller.SendAsync("OnError", "Could not transcribe audio.");
                return;
            }

            await Clients.Caller.SendAsync("OnTranscription", question);

            await Clients.Caller.SendAsync("OnStatus", "Thinking...");
            var answer = await _llm.ChatAsync(question);

            // Send full markdown answer to UI for display
            await Clients.Caller.SendAsync("OnAnswer", answer);

            // Strip markdown before sending to TTS
            var cleanAnswer = StripMarkdown(answer);

            await Clients.Caller.SendAsync("OnStatus", "Synthesising...");
            var audioResponse = await _tts.SynthesiseAsync(cleanAnswer);

            var audioResponseBase64 = Convert.ToBase64String(audioResponse);
            await Clients.Caller.SendAsync("OnAudio", audioResponseBase64);

            await Clients.Caller.SendAsync("OnStatus", "Ready.");
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("OnError", $"Pipeline error: {ex.Message}");
        }
    }
}
