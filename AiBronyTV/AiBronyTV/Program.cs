using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using AiBronyTV.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;

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

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtKey)
    || string.IsNullOrWhiteSpace(jwtIssuer)
    || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("Jwt:Key, Jwt:Issuer and Jwt:Audience must be configured for the AI service.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("bronytv_session", out var cookieToken)
                    && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VerifiedUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("User");
        policy.RequireClaim("email_verified", "true");
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai-chat", httpContext =>
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : userId;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

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
    builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("TestBronyDb"));
}

builder.Services.AddSingleton<Kernel>(_ =>
{
    var kernelBuilder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: modelId,
        apiKey: deepSeekApiKey,
        endpoint: new Uri(endpoint));
#pragma warning restore SKEXP0010

    return kernelBuilder.Build();
});

builder.Services.AddScoped<BotApiService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        // The AI service shares the PostgreSQL database server with BronyTV but owns
        // an isolated schema. Explicit DDL is used because EnsureCreated does not add
        // tables when another DbContext has already created tables in the database.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS ai;

            CREATE TABLE IF NOT EXISTS ai."UserLimits" (
                "SessionId" character varying(64) NOT NULL,
                "Date" timestamp with time zone NOT NULL,
                "Count" integer NOT NULL,
                CONSTRAINT "PK_UserLimits" PRIMARY KEY ("SessionId")
            );

            CREATE TABLE IF NOT EXISTS ai."ChatMessages" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "SessionId" character varying(170) NOT NULL,
                "CharacterId" character varying(32) NOT NULL,
                "Role" character varying(16) NOT NULL,
                "Content" text NOT NULL,
                "Timestamp" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_ChatMessages" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_ChatMessages_SessionId_CharacterId_Timestamp"
                ON ai."ChatMessages" ("SessionId", "CharacterId", "Timestamp");
            """);
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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
var botIds = bots.Select(bot => bot.id).ToHashSet(StringComparer.Ordinal);

app.MapPost("/api/chat/stream", async (ChatRequest request, BotApiService botService, HttpContext context) =>
{
    var sessionId = request.SessionId?.Trim() ?? string.Empty;
    var characterId = request.CharacterId?.Trim().ToLowerInvariant() ?? string.Empty;
    var message = request.Message?.Trim() ?? string.Empty;

    if (sessionId.Length is < 1 or > 128)
    {
        await WriteValidationErrorAsync(context, "Некорректный идентификатор сессии.");
        return;
    }

    if (!botIds.Contains(characterId))
    {
        await WriteValidationErrorAsync(context, "Неизвестный персонаж.");
        return;
    }

    if (message.Length is < 1 or > 2000)
    {
        await WriteValidationErrorAsync(context, "Сообщение должно содержать от 1 до 2000 символов.");
        return;
    }

    if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    // Client-provided session IDs are namespaced by the authenticated user. Daily limits
    // are keyed only by user ID, so clearing localStorage cannot reset the allowance.
    var conversationKey = $"{userId:N}:{sessionId}";
    var limitKey = $"user:{userId:N}";
    var rawUsername = context.User.FindFirstValue("username")?.Trim();
    var userName = string.IsNullOrWhiteSpace(rawUsername) ? "Пользователь" : $"@{rawUsername}";
    var role = context.User.IsInRole("Owner")
        ? "Owner"
        : context.User.IsInRole("Admin")
            ? "Admin"
            : "User";

    context.Response.ContentType = "text/event-stream; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache, no-transform";
    context.Response.Headers.Append("X-Accel-Buffering", "no");

    try
    {
        var stream = botService.SendMessageStreamAsync(
            conversationKey,
            limitKey,
            characterId,
            message,
            userName,
            role,
            context.RequestAborted);

        await foreach (var chunk in stream.WithCancellation(context.RequestAborted))
        {
            var payload = JsonSerializer.Serialize(new { text = chunk.Text, limit = chunk.IsLimit });
            await context.Response.WriteAsync($"data: {payload}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }

        await context.Response.WriteAsync("data: [DONE]\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        // The browser closed the stream; no error frame is needed.
    }
    catch (Exception)
    {
        var errorPayload = JsonSerializer.Serialize(new { error = "Не удалось получить ответ от ИИ-сервиса." });
        await context.Response.WriteAsync($"data: {errorPayload}\n\n");
        await context.Response.Body.FlushAsync();
    }
})
.RequireAuthorization("VerifiedUser")
.RequireRateLimiting("ai-chat");

app.MapGet("/api/bots", () => Results.Json(bots))
    .RequireAuthorization("VerifiedUser");

app.Run();

static Task WriteValidationErrorAsync(HttpContext context, string message)
{
    context.Response.StatusCode = StatusCodes.Status400BadRequest;
    return context.Response.WriteAsJsonAsync(new { message });
}

public sealed record ChatRequest(string? SessionId, string? CharacterId, string? Message);
