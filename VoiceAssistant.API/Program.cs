using Microsoft.EntityFrameworkCore;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Data;
using VoiceAssistant.Data.Repositories;
using VoiceAssistant.Infrastructure.Services;
using VoiceAssistant.Infrastructure.Tools;
using VoiceAssistant.Services.Services;
using VoiceAssistant.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

var ollamaBase = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/";
var timeoutSec = int.Parse(builder.Configuration["Ollama:TimeoutSeconds"] ?? "60");

// SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/assistant.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Repositories
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();

// Domain services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBillService, BillService>();

// Tools
builder.Services.AddScoped<ITool, UpdateStockTool>();
builder.Services.AddScoped<ITool, GetInventoryTool>();
builder.Services.AddScoped<ITool, ShoppingListTool>();

// HTTP clients
builder.Services.AddHttpClient<ILLMService, OllamaService>(client =>
{
    client.BaseAddress = new Uri(ollamaBase);
    client.Timeout = TimeSpan.FromSeconds(timeoutSec);
});

builder.Services.AddHttpClient<ISTTService, STTService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:SttBaseUrl"] ?? "http://localhost:5001/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ITTSService, TTSService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:TtsBaseUrl"] ?? "http://localhost:5002/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IOCRService, OCRService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:OcrBaseUrl"] ?? "http://localhost:5003/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    options.EnableDetailedErrors = true;
});

builder.Services.AddControllers();
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

// Auto create DB on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<VoiceHub>("/voicehub");

app.Run();
