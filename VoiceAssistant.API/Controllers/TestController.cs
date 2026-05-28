using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ILLMService _llm;
    private readonly ISTTService _stt;
    private readonly ITTSService _tts;
    private readonly IEnumerable<ITool> _tools;

    public TestController(ILLMService llm, ISTTService stt, ITTSService tts, IEnumerable<ITool> tools)
    {
        _llm = llm;
        _stt = stt;
        _tts = tts;
        _tools = tools;
    }

    private static string? ExtractJson(string text)
    {
        text = Regex.Replace(text, @"```json\s*", "");
        text = Regex.Replace(text, @"```\s*", "");
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start == -1 || end == -1 || end <= start) return null;
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

            return await tool.ExecuteAsync(toolInput);
        }
        catch
        {
            return null;
        }
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping([FromQuery] string q = "say hello in one sentence")
    {
        var llmAnswer = await _llm.ChatAsync(q, BuildToolsPrompt());
        var toolResult = await TryExecuteToolAsync(llmAnswer);
        var finalAnswer = toolResult ?? llmAnswer;
        return Ok(new { question = q, llm_raw = llmAnswer, answer = finalAnswer });
    }

    [HttpPost("transcribe")]
    public async Task<IActionResult> Transcribe(IFormFile audio)
    {
        if (audio == null || audio.Length == 0)
            return BadRequest(new { error = "no audio file provided" });

        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var text = await _stt.TranscribeAsync(ms.ToArray());
        return Ok(new { text });
    }

    [HttpPost("pipeline")]
    public async Task<IActionResult> Pipeline(IFormFile audio)
    {
        if (audio == null || audio.Length == 0)
            return BadRequest(new { error = "no audio file provided" });

        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);

        var question = await _stt.TranscribeAsync(ms.ToArray());
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest(new { error = "could not transcribe audio" });

        var answer = await _llm.ChatAsync(question, BuildToolsPrompt());
        var toolResult = await TryExecuteToolAsync(answer);
        var finalAnswer = toolResult ?? answer;
        var audioResponse = await _tts.SynthesiseAsync(finalAnswer);

        return File(audioResponse, "audio/wav");
    }
}
