using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

var ollamaBase = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/";
var timeoutSec = int.Parse(builder.Configuration["Ollama:TimeoutSeconds"] ?? "60");

builder.Services.AddHttpClient<ILLMService, OllamaService>(client =>
{
    client.BaseAddress = new Uri(ollamaBase);
    client.Timeout = TimeSpan.FromSeconds(timeoutSec);
});

builder.Services.AddHttpClient<ISTTService, STTService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ITTSService, TTSService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5002/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
