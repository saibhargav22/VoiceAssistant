using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VoiceAssistant.Data;
using Microsoft.EntityFrameworkCore;

namespace VoiceAssistant.API.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;
    private readonly IHostApplicationLifetime _lifetime;

    public SettingsController(
        IConfiguration config,
        IWebHostEnvironment env,
        AppDbContext db,
        IHostApplicationLifetime lifetime)
    {
        _config = config;
        _env = env;
        _db = db;
        _lifetime = lifetime;
    }

    // GET /api/settings
    [HttpGet]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            ollama = new
            {
                baseUrl  = _config["Ollama:BaseUrl"],
                model    = _config["Ollama:Model"],
                timeout  = _config["Ollama:TimeoutSeconds"]
            },
            services = new
            {
                sttUrl = _config["Services:SttUrl"],
                ttsUrl = _config["Services:TtsUrl"],
                ocrUrl = _config["Services:OcrUrl"]
            },
            assistantSettings = new
            {
                ttsEnabled           = bool.Parse(_config["AssistantSettings:TtsEnabled"] ?? "true"),
                voiceSpeed           = double.Parse(_config["AssistantSettings:VoiceSpeed"] ?? "1.0"),
                defaultAudioResponse = bool.Parse(_config["AssistantSettings:DefaultAudioResponse"] ?? "true"),
                billScanMode         = _config["AssistantSettings:BillScanMode"] ?? "ocr"
            }
        });
    }

    // GET /api/settings/models
    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        try
        {
            var ollamaBase = _config["Ollama:BaseUrl"] ?? "http://localhost:11434/";
            using var http = new HttpClient { BaseAddress = new Uri(ollamaBase) };
            var response = await http.GetAsync("api/tags");
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, "Ollama not reachable");

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var models = doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString())
                .ToList();

            return Ok(models);
        }
        catch (Exception ex)
        {
            return StatusCode(502, $"Could not reach Ollama: {ex.Message}");
        }
    }

    // GET /api/settings/dbstats
    [HttpGet("dbstats")]
    public async Task<IActionResult> GetDbStats()
    {
        var itemCount       = await _db.Items.CountAsync();
        var billCount       = await _db.Bills.CountAsync();
        var stockEventCount = await _db.StockEvents.CountAsync();

        var dbPath = _config["Database:Path"] ?? "";
        long fileSizeBytes = 0;
        if (System.IO.File.Exists(dbPath))
            fileSizeBytes = new System.IO.FileInfo(dbPath).Length;

        return Ok(new
        {
            itemCount,
            billCount,
            stockEventCount,
            fileSizeKb = Math.Round(fileSizeBytes / 1024.0, 1)
        });
    }

    // POST /api/settings
    [HttpPost]
    public IActionResult SaveSettings([FromBody] SaveSettingsRequest request)
    {
        try
        {
            var appSettingsPath = Path.Combine(
                Directory.GetCurrentDirectory(), "appsettings.json");

            var json = System.IO.File.ReadAllText(appSettingsPath);
            var doc  = JsonDocument.Parse(json);
            var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

            // Rebuild the sections we manage
            var updated = new
            {
                Logging = root.ContainsKey("Logging") ? root["Logging"] : (object)"{}",
                Ollama = new
                {
                    BaseUrl        = request.OllamaBaseUrl,
                    Model          = request.Model,
                    TimeoutSeconds = request.TimeoutSeconds
                },
                Services = new
                {
                    SttUrl = request.SttUrl,
                    TtsUrl = request.TtsUrl,
                    OcrUrl = request.OcrUrl
                },
                AssistantSettings = new
                {
                    TtsEnabled           = request.TtsEnabled,
                    VoiceSpeed           = request.VoiceSpeed,
                    DefaultAudioResponse = request.DefaultAudioResponse,
                    BillScanMode         = request.BillScanMode
                },
                Database = root.ContainsKey("Database") ? root["Database"] : (object)"{}",
                Kestrel  = root.ContainsKey("Kestrel")  ? root["Kestrel"]  : (object)"{}"
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            System.IO.File.WriteAllText(appSettingsPath, JsonSerializer.Serialize(updated, options));

            return Ok(new { message = "Settings saved. Restart required for model/URL changes." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to save settings: {ex.Message}");
        }
    }

    // POST /api/settings/restart
    [HttpPost("restart")]
    public IActionResult Restart()
    {
        // Graceful stop — process manager (systemd / start.sh) should restart it
        Task.Run(async () =>
        {
            await Task.Delay(500);
            _lifetime.StopApplication();
        });
        return Ok(new { message = "Restarting..." });
    }
}

public class SaveSettingsRequest
{
    public string OllamaBaseUrl      { get; set; } = "http://localhost:11434/";
    public string Model              { get; set; } = "gemma3:4b";
    public int    TimeoutSeconds     { get; set; } = 60;
    public string SttUrl             { get; set; } = "http://localhost:5001/";
    public string TtsUrl             { get; set; } = "http://localhost:5002/";
    public string OcrUrl             { get; set; } = "http://localhost:5003/";
    public bool   TtsEnabled         { get; set; } = true;
    public double VoiceSpeed         { get; set; } = 1.0;
    public bool   DefaultAudioResponse { get; set; } = true;
    public string BillScanMode         { get; set; } = "ocr";
}
