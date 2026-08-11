using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using AiBronyTV.Core;

var builder = WebApplication.CreateBuilder(args);

// Secrets and settings come from environment variables (set in docker-compose.yml / .env).
var deepSeekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
if (string.IsNullOrWhiteSpace(deepSeekApiKey))
{
    throw new InvalidOperationException("DEEPSEEK_API_KEY is not configured.");
}
var rawModel = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL");
var modelId = string.IsNullOrWhiteSpace(rawModel) ? "deepseek-chat" : rawModel.Trim();
var endpoint = Environment.GetEnvironmentVariable("DEEPSEEK_ENDPOINT") ?? "https://api.deepseek.com/v1";

// Database: use PostgreSQL when POSTGRES_HOST is set, otherwise in-memory (local demo only).
var pgHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
if (!string.IsNullOrWhiteSpace(pgHost))
{
    var pgDb = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "AiBronyDb";
    var pgUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
    var pgPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? string.Empty;
    var pgConnection = $"Host={pgHost};Database={pgDb};Username={pgUser};Password={pgPassword}";
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(pgConnection));
}
else
{
    // In-memory database used only for local testing / demo without Postgres.
    builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("TestBronyDb"));
}

builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
#pragma warning disable SKEXP0010
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: modelId,
        apiKey: deepSeekApiKey,
        endpoint: new Uri(endpoint) 
    );
#pragma warning restore SKEXP0010
    
    return kernelBuilder.Build();
});

// Добавляем как Scoped, так как DbContext тоже Scoped
builder.Services.AddScoped<BotApiService>();

var app = builder.Build();

// Авто-создание базы и таблиц при запуске
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapPost("/api/chat/stream", async (ChatRequest request, BotApiService botService, HttpContext ctx) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

    try
    {
        var stream = botService.SendMessageStreamAsync(request.SessionId, request.CharacterId, request.Message);
        
                await foreach (var chunk in stream)
        {
            var payload = JsonSerializer.Serialize(new { text = chunk.Text, limit = chunk.IsLimit });
            await ctx.Response.WriteAsync($"data: {payload}\n\n");
            await ctx.Response.Body.FlushAsync();
        }
        
        await ctx.Response.WriteAsync("data: [DONE]\n\n");
        await ctx.Response.Body.FlushAsync();
    }
    catch (Exception ex)
    {
        var errorPayload = JsonSerializer.Serialize(new { error = ex.Message });
        await ctx.Response.WriteAsync($"data: {errorPayload}\n\n");
        await ctx.Response.Body.FlushAsync();
    }
});

// Метаданные доступных персонажей-ботов (для UI). Аватары раздаёт фронтенд из assets.
var bots = new[]
{
    new { id = "rainbow", name = "Рэйнбоу Дэш", description = "Самая быстрая и дерзкая пегаска Понивилля." },
    new { id = "twilight", name = "Твайлайт Спаркл", description = "Принцесса дружбы и учёный-книжный червь." },
    new { id = "trixie", name = "Трикси", description = "Великая и Могущественная иллюзионистка." },
    new { id = "pinki", name = "Пинки Пай", description = "Неутомимая королева вечеринок и кексов." },
    new { id = "fluttershy", name = "Флаттершай", description = "Добрая и робкая ценительница животных." },
    new { id = "rarity", name = "Рарити", description = "Изысканный единорог-модельер из бутика 'Карусель'." },
    new { id = "applejack", name = "Эпплджек", description = "Надёжная и честная земная пони с фермы." },
    new { id = "starlight", name = "Старлайт Глиммер", description = "Бывшая злодейка, а теперь ученица Искорки." },
    new { id = "sunset", name = "Сансет Шиммер", description = "Крутая рок-звезда из мира людей." },
    new { id = "celestia", name = "Принцесса Селестия", description = "Мудрая правительница Эквестрии, поднимающая солнце." },
    new { id = "luna", name = "Принцесса Луна", description = "Повелительница снов и ночи, хранительница сновидений." },
    new { id = "cadance", name = "Принцесса Каденс", description = "Аликорн любви, правительница Кристальной Империи." }
};

app.MapGet("/api/bots", () => Results.Json(bots));

app.Run();

public record ChatRequest(string SessionId, string CharacterId, string Message);