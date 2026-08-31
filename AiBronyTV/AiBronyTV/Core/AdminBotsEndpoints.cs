using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AiBronyTV.Core;

/// <summary>
/// Админские эндпоинты раздела «ИИ Боты». Закрыты проверкой роли Owner/Admin внутри
/// каждого обработчика (политика "VerifiedUser" дополнительно требует аутентификацию,
/// подтверждённый email и роль "User", как и все существующие admin-эндпоинты сервиса).
///
/// AiBronyTV использует minimal APIs (не MVC-контроллеры), поэтому это идиоматический
/// аналог «AdminBotsController»: отдельный файл с группой маршрутов под /api/admin.
/// </summary>
public static class AdminBotsEndpoints
{
    public static void MapAdminBots(this WebApplication app)
    {
        // Список админских ботов (те же персонажи, но отдельный эндпоинт).
        app.MapGet("/api/admin/bots", () => Results.Json(BotCatalog.Bots))
            .RequireAuthorization("VerifiedUser");

        // Изолированная история админского чата по конкретному персонажу.
        app.MapGet("/api/admin/chat/history", async (string characterId, HttpContext ctx, AppDbContext db) =>
        {
            if (!IsAdmin(ctx)) return Forbidden();

            var sessionId = AdminSessionId(ctx);
            var messages = await db.ChatMessages
                .Where(m => m.SessionId == sessionId && m.CharacterId == characterId && m.IsAdminChat)
                .OrderBy(m => m.Timestamp)
                .Select(m => new { role = m.Role, content = m.Content, timestamp = m.Timestamp })
                .ToListAsync();

            return Results.Ok(messages);
        }).RequireAuthorization("VerifiedUser");

        // Отправка сообщения (SSE-стрим, как публичный /api/chat/stream).
        app.MapPost("/api/admin/chat/stream", async (AdminChatRequest request, HttpContext ctx, BotApiService botService) =>
        {
            if (!IsAdmin(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { message = "Доступ только для владельца или администратора." });
                return;
            }

            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            try
            {
                var stream = botService.SendAdminMessageStreamAsync(
                    AdminSessionId(ctx),
                    request.CharacterId,
                    request.Message,
                    ctx.RequestAborted);

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
        }).RequireAuthorization("VerifiedUser");

        // Очистка админской истории по конкретному персонажу.
        app.MapDelete("/api/admin/chat/history", async (string characterId, HttpContext ctx, AppDbContext db) =>
        {
            if (!IsAdmin(ctx)) return Forbidden();

            var sessionId = AdminSessionId(ctx);
            await db.ChatMessages
                .Where(m => m.SessionId == sessionId && m.CharacterId == characterId && m.IsAdminChat)
                .ExecuteDeleteAsync();

            return Results.Ok(new { cleared = true });
        }).RequireAuthorization("VerifiedUser");
    }

    private static bool IsAdmin(HttpContext ctx) =>
        ctx.User.IsInRole("Owner") || ctx.User.IsInRole("Admin");

    private static IResult Forbidden() =>
        Results.Json(new { message = "Доступ только для владельца или администратора." },
            statusCode: StatusCodes.Status403Forbidden);

    /// <summary>История админского чата привязана к userId (а не к sessionId из localStorage).</summary>
    private static string AdminSessionId(HttpContext ctx)
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Не удалось определить пользователя.");
        }

        return "admin:" + userId;
    }
}

public record AdminChatRequest(string CharacterId, string Message);
