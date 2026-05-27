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

    public TestController(ILLMService llm, ISTTService stt, ITTSService tts)
    {
        _llm = llm;
        _stt = stt;
        _tts = tts;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping([FromQuery] string q = "say hello in one sentence")
    {
        var result = await _llm.ChatAsync(q);
        return Ok(new { question = q, answer = result });
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

        var answer = await _llm.ChatAsync(question);
        var audioResponse = await _tts.SynthesiseAsync(answer);

        return File(audioResponse, "audio/wav");
    }
}
