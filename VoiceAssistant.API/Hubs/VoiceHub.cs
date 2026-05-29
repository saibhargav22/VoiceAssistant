using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
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
        text = Regex.Replace(text, @"```json\s*", "");
        text = Regex.Replace(text, @"```\s*", "");
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        if (start == -1 || end == -1 || end <= start) return null;
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
               "User: what do I have -> {\"tool\": \"get_inventory\", \"input\": \"\"}\n\n" +
               "For general questions unrelated to inventory, reply in plain conversational sentences.";
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

            var systemPrompt    = BuildToolsPrompt();
            var fullResponse    = new StringBuilder();
            var sentenceBuffer  = new StringBuilder();
            bool? isToolCall    = null;
            var peekBuffer      = new StringBuilder();

            // TTS channel: sentences arrive in order and are synthesised sequentially
            var ttsChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
            var ttsTask    = DrainTtsChannelAsync(ttsChannel.Reader);

            await foreach (var token in _llm.StreamAsync(question, systemPrompt))
            {
                fullResponse.Append(token);

                // Determine call type from the first non-whitespace content received
                if (!isToolCall.HasValue)
                {
                    peekBuffer.Append(token);
                    var peek = peekBuffer.ToString().TrimStart();
                    if (peek.Length > 0)
                        isToolCall = peek[0] == '{';
                }

                if (isToolCall == false)
                {
                    // Stream each token to the client for live text display
                    await Clients.Caller.SendAsync("OnToken", token);
                    sentenceBuffer.Append(token);

                    // Flush a complete sentence to TTS as soon as it ends
                    var buf = sentenceBuffer.ToString().TrimEnd();
                    if (buf.Length > 15 && (buf.EndsWith('.') || buf.EndsWith('?') || buf.EndsWith('!')))
                    {
                        var clean = StripMarkdown(sentenceBuffer.ToString().Trim());
                        if (!string.IsNullOrWhiteSpace(clean))
                            await ttsChannel.Writer.WriteAsync(clean);
                        sentenceBuffer.Clear();
                    }
                }
            }

            var fullText = fullResponse.ToString().Trim();

            if (isToolCall != false)
            {
                // Tool call path — execute the tool and TTS its result
                var toolResult  = await TryExecuteToolAsync(fullText);
                var finalAnswer = toolResult ?? StripMarkdown(fullText);

                await Clients.Caller.SendAsync("OnAnswer", finalAnswer);
                await Clients.Caller.SendAsync("OnStatus", "Synthesising...");
                await ttsChannel.Writer.WriteAsync(finalAnswer);
            }
            else
            {
                // Conversational path — flush any trailing partial sentence
                var remaining = sentenceBuffer.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(remaining))
                {
                    var clean = StripMarkdown(remaining);
                    if (!string.IsNullOrWhiteSpace(clean))
                        await ttsChannel.Writer.WriteAsync(clean);
                }
                await Clients.Caller.SendAsync("OnAnswer", StripMarkdown(fullText));
            }

            ttsChannel.Writer.Complete();
            await ttsTask;
            await Clients.Caller.SendAsync("OnStatus", "Ready.");
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("OnError", $"Pipeline error: {ex.Message}");
        }
    }

    /// <summary>
    /// Drains the TTS channel sequentially so audio chunks are always sent in arrival order.
    /// </summary>
    private async Task DrainTtsChannelAsync(ChannelReader<string> reader)
    {
        await foreach (var sentence in reader.ReadAllAsync())
        {
            try
            {
                var audio = await _tts.SynthesiseAsync(sentence);
                await Clients.Caller.SendAsync("OnAudioChunk", Convert.ToBase64String(audio));
            }
            catch { /* a single TTS failure must not abort remaining chunks */ }
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

            var toolName  = toolProp.GetString();
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

