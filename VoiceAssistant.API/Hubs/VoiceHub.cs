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

    private string BuildStockyPrompt()
    {
        var toolList = string.Join("\n", _tools.Select(t => $"- {t.Name}: {t.Description}"));

        return "You are Stocky, a home inventory assistant. You MUST follow these rules strictly:\n\n" +
               "RULE 1: When the user mentions finishing, buying, running out of, or updating any item, " +
               "you MUST respond with ONLY a JSON object. No other text. No explanation.\n" +
               "RULE 2: When the user asks what to buy or about their shopping list, " +
               "respond with ONLY a JSON object.\n" +
               "RULE 3: When the user asks what they have or about their stock, " +
               "respond with ONLY a JSON object.\n" +
               "RULE 4: When the user asks about current date, current time, today's news, " +
               "live scores, weather, prices, or ANY information that changes over time, " +
               "you MUST respond with ONLY a JSON object using the web_search tool. " +
               "NEVER answer these from memory. Your training data is outdated.\n\n" +
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
               "User: show me category 1 -> {\"tool\": \"get_category_items\", \"input\": \"1\"}\n" +
               "User: how much did I spend this month -> {\"tool\": \"get_monthly_spend\", \"input\": \"\"}\n" +
               "User: spending by category -> {\"tool\": \"get_spend_by_category\", \"input\": \"\"}\n" +
               "User: am I spending more than last month -> {\"tool\": \"get_spend_trend\", \"input\": \"\"}\n" +
               "User: what do I spend most on -> {\"tool\": \"get_top_items\", \"input\": \"\"}\n" +
               "User: am I over budget -> {\"tool\": \"get_budget_status\", \"input\": \"\"}\n" +
               "User: what is the news today -> {\"tool\": \"web_search\", \"input\": \"latest news today India\"}\n" +
               "User: current petrol price -> {\"tool\": \"web_search\", \"input\": \"current petrol price India\"}\n" +
               "User: IPL score -> {\"tool\": \"web_search\", \"input\": \"IPL score today\"}\n" +
               "User: what is the current date and time -> {\"tool\": \"web_search\", \"input\": \"current date and time India\"}\n" +
               "User: what time is it -> {\"tool\": \"web_search\", \"input\": \"current time India\"}\n" +
               "User: set budget groceries 5000 -> {\"tool\": \"set_budget\", \"input\": \"groceries, 5000\"}\n" +
               "User: set total budget 15000 -> {\"tool\": \"set_budget\", \"input\": \"total, 15000\"}\n" +
               "User: switch to Nova -> Switch to Nova now and answer in plain text once the mode changes.\n\n" +
               "Do not invent any tool name. Use only the tools listed above. " +
               "If the user asks a question unrelated to inventory or asks for an unsupported operation such as renaming an item, " +
               "do not output JSON. Reply in plain conversational text that you are Stocky, the inventory assistant, and can only help with grocery and stock management.";
    }

    private string BuildNovaPrompt()
    {
        return "You are Nova, a friendly general voice assistant. Answer general questions, help with tasks, and respond naturally. " +
               "If the user asks to switch to Stocky, acknowledge the switch and respond as Stocky after switching. " +
               "If the user asks to switch to Nova, confirm the mode and continue answering generally. " +
               "Do not use markdown, bullet points, asterisks, headers, or special formatting. Keep answers concise and natural sounding.";
    }

    private string GetSystemPrompt(string mode)
    {
        return mode switch
        {
            "Stocky" => BuildStockyPrompt(),
            _ => BuildNovaPrompt(),
        };
    }

    private string GetOrCreateAssistantMode()
    {
        if (!Context.Items.TryGetValue("assistantMode", out var modeObj) || modeObj is not string mode)
        {
            mode = "Nova";
            Context.Items["assistantMode"] = mode;
        }

        return mode;
    }

    private void SetAssistantMode(string mode)
    {
        Context.Items["assistantMode"] = mode;
    }

    public override async Task OnConnectedAsync()
    {
        var mode = GetOrCreateAssistantMode();
        await Clients.Caller.SendAsync("OnModeChanged", mode);
        await base.OnConnectedAsync();
    }

    private async Task<bool> NeedsWebSearchAsync(string question, CancellationToken ct)
    {
        var prompt = "Answer with only YES or NO. No other text.\n" +
                    "Does this question require current, real-time, or recent information " +
                    "that an AI model trained in 2023 would not know accurately?\n\n" +
                    "Question: " + question;

        var result = await _llm.ChatAsync(prompt, null, ct);
        return result.Trim().StartsWith("Y", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryAnswerDirectly(string question)
{
    var q = question.ToLowerInvariant();
    if (q.Contains("what time") || q.Contains("current time") ||
        q.Contains("what date") || q.Contains("today's date") ||
        q.Contains("what day")  || q.Contains("current date"))
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        return $"The current date and time is {now:dddd, dd MMMM yyyy} at {now:hh:mm tt} IST.";
    }
    return null;
}

    private bool TrySwitchAssistantMode(string text, out string newMode, out string response)
    {
        var normalized = Regex.Replace(text.Trim().ToLowerInvariant(), @"[^\w\s]", "");

        if (Regex.IsMatch(normalized, @"\b(switch|change|use|go|return|back)\b.*\b(stocky)\b"))
        {
            newMode = "Stocky";
            if (GetOrCreateAssistantMode() == newMode)
            {
                response = "Already in Stocky mode.";
                return true;
            }

            response = "Switched to Stocky. I am now Stocky, your inventory assistant.";
            return true;
        }

        if (Regex.IsMatch(normalized, @"\b(switch|change|use|go|return|back)\b.*\b(nova)\b"))
        {
            newMode = "Nova";
            if (GetOrCreateAssistantMode() == newMode)
            {
                response = "Already in Nova mode.";
                return true;
            }

            response = "Switched to Nova. I am now Nova, your general assistant.";
            return true;
        }

        newMode = GetOrCreateAssistantMode();
        response = string.Empty;
        return false;
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

            var mode = GetOrCreateAssistantMode();
            if (TrySwitchAssistantMode(question, out var newMode, out var switchResponse))
            {
                SetAssistantMode(newMode);
                await Clients.Caller.SendAsync("OnModeChanged", newMode);
                await Clients.Caller.SendAsync("OnAnswer", switchResponse);
                await SendAudioAsync(switchResponse);
                await Clients.Caller.SendAsync("OnStatus", "Ready.");
                return;
            }

            // Date/time — answer from server clock
            var directAnswer = TryAnswerDirectly(question);
            if (directAnswer != null)
            {
                await Clients.Caller.SendAsync("OnAnswer", directAnswer);
                await SendAudioAsync(directAnswer);
                await Clients.Caller.SendAsync("OnStatus", "Ready.");
                return;
            }

            // Web search classifier — only in Nova mode
            // Stocky handles its own tool routing
            if (mode == "Nova" && await NeedsWebSearchAsync(question, ct: default))
            {
                await Clients.Caller.SendAsync("OnStatus", "Searching the web...");
                var searchJson = "{\"tool\": \"web_search\", \"input\": \"" + question.Replace("\"", "'") + "\"}";
                var searchResult = await TryExecuteToolAsync(searchJson);
                var searchAnswer = searchResult ?? "I could not find results for that.";
                await Clients.Caller.SendAsync("OnAnswer", searchAnswer);
                await SendAudioAsync(searchAnswer);
                await Clients.Caller.SendAsync("OnStatus", "Ready.");
                return;
            }

            var systemPrompt = GetSystemPrompt(mode);
            var answer = await _llm.ChatAsync(question, systemPrompt);

            var toolResult = await TryExecuteToolAsync(answer);
            var finalAnswer = toolResult ?? answer;
            if (toolResult == null && ExtractJson(answer) != null && mode == "Stocky")
            {
                finalAnswer = "I am Stocky, the inventory assistant. I can only help with grocery and stock management.";
            }

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

            var mode = GetOrCreateAssistantMode();
            if (TrySwitchAssistantMode(text, out var newMode, out var switchResponse))
            {
                SetAssistantMode(newMode);
                await Clients.Caller.SendAsync("OnModeChanged", newMode);
                await Clients.Caller.SendAsync("OnAnswer", switchResponse);
                if (audioResponse && TtsEnabled)
                {
                    await SendAudioAsync(switchResponse);
                }
                await Clients.Caller.SendAsync("OnStatus", "Ready.");
                return;
            }

            // Date/time — answer from server clock
            var directAnswer = TryAnswerDirectly(text);
            if (directAnswer != null)
            {
                await Clients.Caller.SendAsync("OnAnswer", directAnswer);
                if (audioResponse && TtsEnabled) await SendAudioAsync(directAnswer);
                await Clients.Caller.SendAsync("OnStatus", "Ready.");
                return;
            }

            // Web search classifier — only in Nova mode
            if (mode == "Nova" && await NeedsWebSearchAsync(text, ct: default))
            {
                await Clients.Caller.SendAsync("OnStatus", "Searching the web...");
                var searchJson = "{\"tool\": \"web_search\", \"input\": \"" + text.Replace("\"", "'") + "\"}";
                var searchResult = await TryExecuteToolAsync(searchJson);
                var searchAnswer = searchResult ?? "I could not find results for that.";
                await Clients.Caller.SendAsync("OnAnswer", searchAnswer);
                if (audioResponse && TtsEnabled) await SendAudioAsync(searchAnswer);
                await Clients.Caller.SendAsync("OnStatus", "Ready.");
                return;
            }

            var systemPrompt = GetSystemPrompt(mode);
            var llmResponse = await _llm.ChatAsync(text, systemPrompt);

            var toolResult = await TryExecuteToolAsync(llmResponse);
            var finalAnswer = toolResult ?? llmResponse;
            if (toolResult == null && ExtractJson(llmResponse) != null && mode == "Stocky")
            {
                finalAnswer = "I am Stocky, the inventory assistant. I can only help with grocery and stock management.";
            }

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
            if (tool == null)
            {
                await Clients.Caller.SendAsync("OnStatus", $"Tool not found: {toolName}. Available: {string.Join(", ", _tools.Select(t => t.Name))}");
                return null;
            }

            await Clients.Caller.SendAsync("OnStatus", $"Running {toolName}...");
            return await tool.ExecuteAsync(toolInput);
        }
        catch
        {
            return null;
        }
    }
}
