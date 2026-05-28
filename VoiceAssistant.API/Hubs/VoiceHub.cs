using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.API.Hubs;

public class VoiceHub : Hub
{
    private readonly ISTTService _stt;
    private readonly ILLMService _llm;
    private readonly ITTSService _tts;
    private readonly IEnumerable<ITool> _tools;

    public VoiceHub(ISTTService stt, ILLMService llm, ITTSService tts, IEnumerable<ITool> tools)
    {
        _stt = stt;
        _llm = llm;
        _tts = tts;
        _tools = tools;
    }

    private static string StripMarkdown(string text)
    {
        text = Regex.Replace(text, @"\*{1,3}(.+?)\*{1,3}", "$1");
        text = Regex.Replace(text, @"^#{1,6}\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*[\*\-•]\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*\d+\.\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"```[\s\S]*?```", "");
        text = Regex.Replace(text, @"`(.+?)`", "$1");
        text = Regex.Replace(text, @"\[(.+?)\]\(.+?\)", "$1");
        text = Regex.Replace(text, @"\n{2,}", " ");
        text = text.Replace("\n", " ").Trim();
        return text;
    }

    private static string? ExtractJson(string text)
    {
        // Strip markdown code fences
        text = Regex.Replace(text, @"```json\s*", "");
        text = Regex.Replace(text, @"```\s*", "");

        // Find first { to last }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start == -1 || end == -1 || end <= start)
            return null;

        return text.Substring(start, end - start + 1).Trim();
    }

    private string BuildToolsPrompt()
    {
        var toolList = string.Join("\n", _tools.Select(t => $"- {t.Name}: {t.Description}"));

        return "You are a helpful home voice assistant with access to these tools:\n" +
               toolList + "\n\n" +
               "If the user's request requires a tool, respond ONLY with this exact JSON format:\n" +
               "{\"tool\": \"tool_name\", \"input\": \"tool_input\"}\n\n" +
               "For update_stock, input format is: \"item_name, quantity_change, note\"\n" +
               "Example for rice finished: {\"tool\": \"update_stock\", \"input\": \"rice, -1, finished\"}\n" +
               "Example for shopping list: {\"tool\": \"get_shopping_list\", \"input\": \"\"}\n" +
               "Example for checking stock: {\"tool\": \"get_inventory\", \"input\": \"\"}\n\n" +
               "If no tool is needed, reply in plain conversational sentences without markdown. " +
               "Keep answers concise and natural sounding.";
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

            var systemPrompt = BuildToolsPrompt();
            var answer = await _llm.ChatAsync(question, systemPrompt);

            var toolResult = await TryExecuteToolAsync(answer);
            var finalAnswer = toolResult ?? answer;

            await Clients.Caller.SendAsync("OnAnswer", finalAnswer);

            var cleanAnswer = StripMarkdown(finalAnswer);
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

    private async Task<string?> TryExecuteToolAsync(string llmResponse)
    {
        try
        {
            var json = ExtractJson(llmResponse);
            if (json == null) return null;

            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tool", out var toolProp)) return null;

            var toolName = toolProp.GetString();
            var toolInput = doc.RootElement.TryGetProperty("input", out var inputProp)
                ? inputProp.GetString() ?? ""
                : "";

            var tool = _tools.FirstOrDefault(t => t.Name == toolName);
            if (tool == null) return null;

            await Clients.Caller.SendAsync("OnStatus", $"Running {toolName}...");
            return await tool.ExecuteAsync(toolInput);
        }
        catch
        {
            return null;
        }
    }
}
