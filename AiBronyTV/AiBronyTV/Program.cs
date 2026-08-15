using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AiBronyTV.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

// JWT session validation — same signing key as the main BronyTV backend so the AI service
// can validate the HttpOnly bronytv_session cookie (shared via docker-compose JWT_KEY).
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
            ClockSkew = TimeSpan.FromMinutes(1),
            // The main backend issues roles as ClaimTypes.Role; keep that as the role claim type.
            RoleClaimType = ClaimTypes.Role
        };

        // Read the token from the same cookie as the main site, not just from
        // the Authorization header, so owner/admin overrides keep working.
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

// Chat, the bot list and premium-key activation all require a signed-in user with a
// confirmed email. Roles (Owner/Admin) are resolved later from ctx.User to lift limits.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VerifiedUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("User");
        policy.RequireClaim("email_verified", "true");
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

app.UseAuthentication();
app.UseAuthorization();

// Авто-создание базы и таблиц при запуске
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Postgres-only schema upgrades (InMemory provider has no raw SQL support).
    if (!string.IsNullOrWhiteSpace(pgHost) && db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
    {
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE ai.\"UserLimits\" ADD COLUMN IF NOT EXISTS \"PremiumUntil\" timestamp with time zone;");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS ai.\"PremiumKeys\" " +
            "(\"Key\" character varying(32) NOT NULL, \"IsUsed\" boolean NOT NULL, " +
            "CONSTRAINT \"PK_PremiumKeys\" PRIMARY KEY (\"Key\"));");
    }
}

app.MapPost("/api/chat/stream", async (ChatRequest request, BotApiService botService, HttpContext ctx) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

        try
    {
        // Resolve the user's role from the authentication context (if authenticated).
        // Owner and Admin bypass message limits entirely.
        var role = ctx.User.IsInRole("Owner")
            ? "Owner"
            : ctx.User.IsInRole("Admin")
                ? "Admin"
                : null;

        var stream = botService.SendMessageStreamAsync(
            request.SessionId,
            request.SessionId,
            request.CharacterId,
            request.Message,
            role: role);
        
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
})
.RequireAuthorization("VerifiedUser");

// Активация премиум-ключа (Boosty). Пользователь вводит одноразовый ключ,
// и его лимит на 30 дней повышается до 200 сообщений.
app.MapPost("/api/bots/activate", async (ActivateRequest request, AppDbContext db) =>
{
    var key = request.Key?.Trim();
    if (string.IsNullOrWhiteSpace(key))
    {
        return Results.BadRequest(new { message = "Ключ не указан." });
    }

    var premiumKey = await db.PremiumKeys.FirstOrDefaultAsync(item => item.Key == key);
    if (premiumKey == null || premiumKey.IsUsed)
    {
        return Results.BadRequest(new { message = "Неверный или уже использованный ключ." });
    }

        // Сессия текущего пользователя — ограничения по лимитам привязаны к sessionId.
    var limitKey = request.SessionId;
    if (string.IsNullOrWhiteSpace(limitKey))
    {
        return Results.BadRequest(new { message = "Не указана сессия пользователя." });
    }

    var limitEntry = await db.UserLimits.FirstOrDefaultAsync(item => item.SessionId == limitKey);
    if (limitEntry == null)
    {
        limitEntry = new UserLimitEntity { SessionId = limitKey, Date = DateTime.UtcNow, Count = 0 };
        db.UserLimits.Add(limitEntry);
    }

        premiumKey.IsUsed = true;
    limitEntry.PremiumUntil = DateTime.UtcNow.AddDays(30);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Премиум активирован на 30 дней! Лимит 200 сообщений." });
})
.RequireAuthorization("VerifiedUser");

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

app.MapGet("/api/bots", () => Results.Json(bots))
    .RequireAuthorization("VerifiedUser");

app.Run();

public record ChatRequest(string SessionId, string CharacterId, string Message);
public record ActivateRequest(string Key, string? SessionId);