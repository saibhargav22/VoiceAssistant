using Microsoft.EntityFrameworkCore;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Data;
using VoiceAssistant.Data.Repositories;
using VoiceAssistant.Infrastructure.Services;
using VoiceAssistant.Infrastructure.Tools;
using VoiceAssistant.Services.Services;
using VoiceAssistant.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

var ollamaBase  = builder.Configuration["Ollama:BaseUrl"]        ?? "http://localhost:11434/";
var timeoutSec  = int.Parse(builder.Configuration["Ollama:TimeoutSeconds"] ?? "60");
var sttUrl      = builder.Configuration["Services:SttUrl"]       ?? "http://localhost:5001/";
var ttsUrl      = builder.Configuration["Services:TtsUrl"]       ?? "http://localhost:5002/";
var ocrUrl      = builder.Configuration["Services:OcrUrl"]       ?? "http://localhost:5003/";
var dbPath      = builder.Configuration["Database:Path"]         ?? "/home/viddharth/VoiceAssistant/data/assistant.db";

// SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Repositories
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();

// Domain services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBillService, BillService>();

builder.Services.AddScoped<IFinancialRepository, FinancialRepository>();
builder.Services.AddScoped<IFinancialService, FinancialService>();

// Tools
builder.Services.AddScoped<ITool, UpdateStockTool>();
builder.Services.AddScoped<ITool, GetInventoryTool>();
builder.Services.AddScoped<ITool, ShoppingListTool>();
builder.Services.AddScoped<ITool, FindItemTool>();
builder.Services.AddScoped<ITool, UpdateLocationTool>();
builder.Services.AddScoped<ITool, GetCupboardContentsTool>();
builder.Services.AddScoped<ITool, GetCategoryItemsTool>();
builder.Services.AddScoped<ITool, GetMonthlySpendTool>();
builder.Services.AddScoped<ITool, GetSpendByCategoryTool>();
builder.Services.AddScoped<ITool, GetSpendTrendTool>();
builder.Services.AddScoped<ITool, GetTopItemsTool>();
builder.Services.AddScoped<ITool, GetBudgetStatusTool>();
builder.Services.AddScoped<ITool, SetBudgetTool>();
builder.Services.AddScoped<ITool, WebSearchTool>();

// HTTP clients
builder.Services.AddHttpClient<ILLMService, OllamaService>(client =>
{
    client.BaseAddress = new Uri(ollamaBase);
    client.Timeout = TimeSpan.FromSeconds(timeoutSec);
});

builder.Services.AddHttpClient<ISTTService, STTService>(client =>
{
    client.BaseAddress = new Uri(sttUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ITTSService, TTSService>(client =>
{
    client.BaseAddress = new Uri(ttsUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IOCRService, OCRService>(client =>
{
    client.BaseAddress = new Uri(ocrUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    options.EnableDetailedErrors = true;
});

var searxngUrl = builder.Configuration["Search:SearxngUrl"] ?? "http://localhost:8080/";

builder.Services.AddHttpClient<IWebSearchService, SearXNGService>(client =>
{
    client.BaseAddress = new Uri(searxngUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
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
