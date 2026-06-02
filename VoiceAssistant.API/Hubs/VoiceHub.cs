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
    private readonly IConfiguration _config;

    public VoiceHub(ISTTService stt, ILLMService llm, ITTSService tts, IEnumerable<ITool> tools, IConfiguration config)
    {
        _stt = stt;
        _llm = llm;
        _tts = tts;
        _tools = tools;
        _config = config;
    }

    private bool TtsEnabled => bool.Parse(_config["AssistantSettings:TtsEnabled"] ?? "true");
    private double VoiceSpeed => double.Parse(_config["AssistantSettings:VoiceSpeed"] ?? "1.0");

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
        text = Regex.Replace(text, @"```json\s*", "");
        text = Regex.Replace(text, @"```\s*", "");

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start == -1 || end == -1 || end <= start)
            return null;

        return text.Substring(start, end - start + 1).Trim();
    }

    private string BuildToolsPrompt()
    {
        var toolList = string.Join("\n", _tools.Select(t => $"- {t.Name}: {t.Description}"));

        return "You are a home inventory assistant. You MUST follow these rules strictly:\n\n" +
               "RULE 1: When the user mentions finishing, buying, running out of, or updating any item, " +
               "you MUST respond with ONLY a JSON object. No other text. No explanation.\n" +
               "RULE 2: When the user asks what to buy or about their shopping list, " +
               "respond with ONLY a JSON object.\n" +
               "RULE 3: When the user asks what they have or about their stock, " +
               "respond with ONLY a JSON object.\n\n" +
               "Available tools:\n" + toolList + "\n\n" +
               "JSON format (respond with ONLY this, nothing else):\n" +
               "{\"tool\": \"tool_name\", \"input\": \"tool_input\"}\n\n" +
               "update_stock input format: item_name, quantity_change, note\n" +
               "Positive number = bought/restocked. Negative number = used/finished.\n\n" +
               "EXAMPLES - respond exactly like this:\n" +
               "User: I finished the rice -> {\"tool\": \"update_stock\", \"input\": \"rice, -1, finished\"}\n" +
               "User: I bought 2kg rice -> {\"tool\": \"update_stock\", \"input\": \"rice, 2, purchased\"}\n" +
               "User: going shopping -> {\"tool\": \"get_shopping_list\", \"input\": \"\"}\n" +
               "User: what do I have -> {\"tool\": \"get_inventory\", \"input\": \"\"}\n" +
               "User: where is the rice -> {\"tool\": \"find_item\", \"input\": \"rice\"}\n" +
               "User: rice is in cupboard C1 slot 2 -> {\"tool\": \"update_location\", \"input\": \"rice, C1, 2\"}\n" +
               "User: what's in cupboard C1 -> {\"tool\": \"get_cupboard_contents\", \"input\": \"C1\"}\n" +
               "User: show me category 1 -> {\"tool\": \"get_category_items\", \"input\": \"1\"}\n\n" +
               "For general questions unrelated to inventory, reply in plain conversational sentences.";
    }

    private async Task SendAudioAsync(string text)
    {
        if (!TtsEnabled) return;

        var clean = StripMarkdown(text);
        await Clients.Caller.SendAsync("OnStatus", "Synthesising...");
        var audioBytes = await _tts.SynthesiseAsync(clean, VoiceSpeed);
        var audioBase64 = Convert.ToBase64String(audioBytes);
        await Clients.Caller.SendAsync("OnAudio", audioBase64);
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

            await SendAudioAsync(finalAnswer);

            await Clients.Caller.SendAsync("OnStatus", "Ready.");
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("OnError", $"Pipeline error: {ex.Message}");
        }
    }

    public async Task ProcessText(string text, bool audioResponse)
    {
        try
        {
            await Clients.Caller.SendAsync("OnTranscription", text);
            await Clients.Caller.SendAsync("OnStatus", "Thinking...");

            var systemPrompt = BuildToolsPrompt();
            var llmResponse = await _llm.ChatAsync(text, systemPrompt);

            var toolResult = await TryExecuteToolAsync(llmResponse);
            var finalAnswer = toolResult ?? llmResponse;

            await Clients.Caller.SendAsync("OnAnswer", finalAnswer);

            // Respect both the per-session toggle AND the global TtsEnabled setting
            if (audioResponse && TtsEnabled)
            {
                await SendAudioAsync(finalAnswer);
            }

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
