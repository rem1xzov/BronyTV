using System.Security.Claims;
using System.Security.Cryptography;
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

// Server-to-server: базовый URL основного бэкенда + общий внутренний ключ для защиты
// внутренних эндпоинтов (по аналогии с JWT_KEY в docker-compose).
var bronyBackendUrl = Environment.GetEnvironmentVariable("BRONYTV_BACKEND_URL") ?? "http://brony-backend:5000";
var internalKey = Environment.GetEnvironmentVariable("BRONYTV_INTERNAL_KEY") ?? string.Empty;

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

// HttpClient для server-to-server звонков в основной BronyTV-бэкенд.
builder.Services.AddHttpClient("BronyBackend", client =>
{
    client.BaseAddress = new Uri(bronyBackendUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

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

app.MapPost("/api/chat/stream", async (ChatRequest request, BotApiService botService, HttpContext ctx, IHttpClientFactory httpClientFactory) =>
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

        // UserId из JWT (ClaimTypes.NameIdentifier) — основной site-пользователь.
        var userIdRaw = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(userIdRaw, out var bronyUserId);

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

        // Best-effort: логируем факт общения с ботом в основной backend.
        // Передаём ТОЛЬКО UserId и имя бота (characterId), НИКОГДА текст сообщения.
        if (bronyUserId != Guid.Empty && !string.IsNullOrWhiteSpace(internalKey))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var client = httpClientFactory.CreateClient("BronyBackend");
                    var payload = JsonSerializer.Serialize(new
                    {
                        userId = bronyUserId,
                        characterId = request.CharacterId
                    });
                    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    content.Headers.Add("X-Internal-Key", internalKey);
                    await client.PostAsync("/api/internal/activity/bot-chat", content);
                }
                catch
                {
                    // Логирование бот-активности не должно ломать чат.
                }
            });
        }
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

// Статус премиума для текущей сессии — фронтенд решает, показывать "+" или галочку.
app.MapGet("/api/bots/premium-status", async (string sessionId, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(sessionId))
    {
        return Results.Ok(new { isActive = false });
    }

    var limitEntry = await db.UserLimits.FirstOrDefaultAsync(item => item.SessionId == sessionId);
    if (limitEntry?.PremiumUntil == null || limitEntry.PremiumUntil <= DateTime.UtcNow)
    {
        return Results.Ok(new { isActive = false });
    }

    var daysLeft = (int)Math.Ceiling((limitEntry.PremiumUntil.Value - DateTime.UtcNow).TotalDays);
    return Results.Ok(new
    {
        isActive = true,
        expiresAt = limitEntry.PremiumUntil.Value.ToString("O"),
        daysLeft
    });
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
        new { id = "cadance", name = "Принцесса Каденс", description = "Аликорн любви, правительница Кристальной Империи." },
    new { id = "applebloom", name = "Эппл Блум", description = "Младшая сестра Эпплджек, ищущая свой талант." },
    new { id = "sweetiebelle", name = "Свити Бель", description = "Сестренка Рарити. Хорошо поет, но часто косячит." },
    new { id = "scootaloo", name = "Скуталу", description = "Сорвиголова на скутере и фанатка Радуги Дэш." },
    new { id = "derpy", name = "Дерпи", description = "Добрая почтальонша, которая очень любит маффины." },
    new { id = "discord", name = "Дискорд", description = "Бывший дух хаоса. Обожает абсурд и розыгрыши." },
    new { id = "cozyglow", name = "Коузи Глоу", description = "Самая милая пони... с манией величия." },
    new { id = "octavia", name = "Октавия", description = "Изысканная виолончелистка из Кантерлота." },
    new { id = "djpon3", name = "DJ Pon-3", description = "Крутая тусовщица, общающаяся на сленге." },
    new { id = "shiningarmor", name = "Шайнинг Армор", description = "Капитан Королевской Стражи и гик." },
    new { id = "narrator", name = "Рассказчик (RPG)", description = "Опиши своего персонажа, и Рассказчик создаст для тебя сюжет в Эквестрии!" }
};

app.MapGet("/api/bots", () => Results.Json(bots))
    .RequireAuthorization("VerifiedUser");

// Генерация одноразового премиум-ключа (для выдачи покупателям Boosty).
// Только Owner/Admin. Ключ генерируется криптографически стойким ГПСЧ.
app.MapPost("/api/admin/premium-keys/generate", async (AppDbContext db, HttpContext ctx) =>
{
    var isOwner = ctx.User.IsInRole("Owner");
    var isAdmin = ctx.User.IsInRole("Admin");
    if (!isOwner && !isAdmin)
    {
        return Results.Json(new { message = "Доступ только для владельца или администратора." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    // Безопасный алфавит без похожих символов (нет 0/O, 1/I/l), чтобы ключ
    // можно было без ошибок перепечатать вручную.
    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
    var keyChars = new char[20];
    for (var i = 0; i < keyChars.Length; i++)
    {
                keyChars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
    var key = new string(keyChars);

    db.PremiumKeys.Add(new PremiumKeyEntity { Key = key, IsUsed = false });
    await db.SaveChangesAsync();

    return Results.Ok(new { key });
})
.RequireAuthorization("VerifiedUser");

// Список неиспользованных премиум-ключей (для выдачи покупателям Boosty).
// Только Owner/Admin. Возвращает все ключи с IsUsed == false.
app.MapGet("/api/admin/premium-keys/list", async (AppDbContext db, HttpContext ctx) =>
{
    var isOwner = ctx.User.IsInRole("Owner");
    var isAdmin = ctx.User.IsInRole("Admin");
    if (!isOwner && !isAdmin)
    {
        return Results.Json(new { message = "Доступ только для владельца или администратора." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    var keys = await db.PremiumKeys
        .Where(item => !item.IsUsed)
        .Select(item => item.Key)
        .ToListAsync();

        return Results.Ok(new { keys, total = keys.Count });
})
.RequireAuthorization("VerifiedUser");

// Очистка истории переписки для конкретного персонажа (sessionId + characterId).
// Используется кнопкой «Очистить чат» на фронтенде, чтобы бот забыл прошлый разговор.
app.MapDelete("/api/chat/history", async (string sessionId, string characterId, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(characterId))
    {
        return Results.BadRequest(new { message = "Не указаны sessionId или characterId." });
    }

    await db.ChatMessages
        .Where(message => message.SessionId == sessionId && message.CharacterId == characterId)
        .ExecuteDeleteAsync();

    return Results.Ok(new { cleared = true });
})
.RequireAuthorization("VerifiedUser");

app.Run();

public record ChatRequest(string SessionId, string CharacterId, string Message);
public record ActivateRequest(string Key, string? SessionId);