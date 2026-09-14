using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AiBronyTV.Core;

/// <summary>
/// CRUD и отправка сообщений для групповых чатов с несколькими ботами.
/// Параметр <paramref name="isAdminChat"/> задаёт, к какому разделу относится чат
/// (публичный или админский) — от этого зависят промпты и роль доступа.
/// </summary>
public static class GroupChatEndpoints
{
    public static void MapGroupChats(this WebApplication app, string prefix, bool isAdminChat)
    {
        // Список групповых чатов текущего пользователя.
        app.MapGet(prefix, async (HttpContext ctx, AppDbContext db) =>
        {
            if (!Authorize(ctx, isAdminChat, out var userId, out var error)) return error!;

            var chats = await db.GroupChats
                .Where(g => g.UserId == userId && g.IsAdminChat == isAdminChat)
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.CreatedAt,
                    participantCount = db.GroupChatParticipants.Count(p => p.GroupChatId == g.Id)
                })
                .ToListAsync();

            return Results.Ok(chats);
        }).RequireAuthorization("VerifiedUser");

        // Создание группового чата.
        app.MapPost(prefix, async (CreateGroupChatRequest request, HttpContext ctx, AppDbContext db) =>
        {
            if (!Authorize(ctx, isAdminChat, out var userId, out var error)) return error!;

            var name = string.IsNullOrWhiteSpace(request?.Name) ? "Групповой чат" : request.Name.Trim();
            var characterIds = NormalizeCharacterIds(request?.CharacterIds);
            if (characterIds.Count == 0)
            {
                return Results.BadRequest(new { message = "Выберите хотя бы одного персонажа." });
            }

            var chat = new GroupChatEntity
            {
                UserId = userId,
                Name = name,
                IsAdminChat = isAdminChat,
                CreatedAt = DateTime.UtcNow
            };
            db.GroupChats.Add(chat);
            await db.SaveChangesAsync();

            foreach (var characterId in characterIds)
            {
                db.GroupChatParticipants.Add(new GroupChatParticipantEntity
                {
                    GroupChatId = chat.Id,
                    CharacterId = characterId,
                    AddedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();

            return Results.Ok(new { chat.Id, chat.Name });
        }).RequireAuthorization("VerifiedUser");

        // Детали чата: участники + история сообщений.
        app.MapGet(prefix + "/{id:int}", async (int id, HttpContext ctx, AppDbContext db) =>
        {
            if (!Authorize(ctx, isAdminChat, out var userId, out var error)) return error!;

            var chat = await db.GroupChats
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId && g.IsAdminChat == isAdminChat);
            if (chat == null)
            {
                return Results.NotFound(new { message = "Групповой чат не найден." });
            }

            var participants = await db.GroupChatParticipants
                .Where(p => p.GroupChatId == id)
                .OrderBy(p => p.AddedAt)
                .ThenBy(p => p.CharacterId)
                .Select(p => p.CharacterId)
                .ToListAsync();

            var messages = await db.GroupChatMessages
                .Where(m => m.GroupChatId == id)
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .Select(m => new { m.Id, m.SenderType, m.SenderCharacterId, m.Content, m.CreatedAt })
                .ToListAsync();

            return Results.Ok(new { chat.Id, chat.Name, participants, messages });
        }).RequireAuthorization("VerifiedUser");

        // Изменить состав участников (заменить список целиком).
        app.MapPut(prefix + "/{id:int}/participants", async (int id, SetParticipantsRequest request, HttpContext ctx, AppDbContext db) =>
        {
            if (!Authorize(ctx, isAdminChat, out var userId, out var error)) return error!;

            var chat = await db.GroupChats
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId && g.IsAdminChat == isAdminChat);
            if (chat == null)
            {
                return Results.NotFound(new { message = "Групповой чат не найден." });
            }

            var characterIds = NormalizeCharacterIds(request?.CharacterIds);
            if (characterIds.Count == 0)
            {
                return Results.BadRequest(new { message = "В чате должен остаться хотя бы один персонаж." });
            }

            var existing = await db.GroupChatParticipants
                .Where(p => p.GroupChatId == id)
                .ToListAsync();
            db.GroupChatParticipants.RemoveRange(existing);

            var now = DateTime.UtcNow;
            foreach (var characterId in characterIds)
            {
                db.GroupChatParticipants.Add(new GroupChatParticipantEntity
                {
                    GroupChatId = id,
                    CharacterId = characterId,
                    AddedAt = now
                });
            }
            await db.SaveChangesAsync();

            return Results.Ok(new { updated = true, characterIds });
        }).RequireAuthorization("VerifiedUser");

        // Удалить групповой чат целиком.
        app.MapDelete(prefix + "/{id:int}", async (int id, HttpContext ctx, AppDbContext db) =>
        {
            if (!Authorize(ctx, isAdminChat, out var userId, out var error)) return error!;

            var chat = await db.GroupChats
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId && g.IsAdminChat == isAdminChat);
            if (chat == null)
            {
                return Results.NotFound(new { message = "Групповой чат не найден." });
            }

            await db.GroupChatMessages.Where(m => m.GroupChatId == id).ExecuteDeleteAsync();
            await db.GroupChatParticipants.Where(p => p.GroupChatId == id).ExecuteDeleteAsync();
            db.GroupChats.Remove(chat);
            await db.SaveChangesAsync();

            return Results.Ok(new { deleted = true });
        }).RequireAuthorization("VerifiedUser");

        // Отправка сообщения: боты отвечают по очереди (SSE-стрим).
        app.MapPost(prefix + "/{id:int}/messages/stream", async (
            int id,
            SendGroupMessageRequest request,
            HttpContext ctx,
            AppDbContext db,
            BotApiService botService) =>
        {
            if (!Authorize(ctx, isAdminChat, out var userId, out var error)) return error!;

            var chat = await db.GroupChats
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId && g.IsAdminChat == isAdminChat);
            if (chat == null)
            {
                return Results.NotFound(new { message = "Групповой чат не найден." });
            }

            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            var role = ctx.User.IsInRole("Owner")
                ? "Owner"
                : ctx.User.IsInRole("Admin")
                    ? "Admin"
                    : null;

            try
            {
                var stream = botService.SendGroupMessageStreamAsync(
                    id,
                    userId,
                    request?.Message ?? string.Empty,
                    isAdminChat,
                    role: role,
                    cancellationToken: ctx.RequestAborted);

                await foreach (var chunk in stream)
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        text = chunk.Text,
                        limit = chunk.IsLimit,
                        userMessageId = chunk.UserMessageId,
                        characterId = chunk.CharacterId,
                        @event = chunk.Event
                    });
                    await ctx.Response.WriteAsync($"data: {payload}\n\n");
                    await ctx.Response.Body.FlushAsync();
                }

                await ctx.Response.WriteAsync("data: [DONE]\n\n");
                await ctx.Response.Body.FlushAsync();
            }
            catch (OperationCanceledException) when (!ctx.RequestAborted.IsCancellationRequested)
            {
                Console.WriteLine("[deepseek-timeout] group chat stream timed out");
                var errorPayload = JsonSerializer.Serialize(new { error = "Бот сейчас недоступен, попробуйте позже.", code = "timeout" });
                await ctx.Response.WriteAsync($"data: {errorPayload}\n\n");
                await ctx.Response.Body.FlushAsync();
            }
            catch (OperationCanceledException)
            {
                // Клиент отключился — нечего писать.
            }
            catch (Exception ex)
            {
                var errorPayload = JsonSerializer.Serialize(new { error = ex.Message });
                await ctx.Response.WriteAsync($"data: {errorPayload}\n\n");
                await ctx.Response.Body.FlushAsync();
            }

            return Results.Empty;
        }).RequireAuthorization("VerifiedUser");
    }

    private static bool Authorize(HttpContext ctx, bool isAdminChat, out string userId, out IResult? error)
    {
        userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
        {
            error = Results.BadRequest(new { message = "Не удалось определить пользователя." });
            return false;
        }

        if (isAdminChat && !(ctx.User.IsInRole("Owner") || ctx.User.IsInRole("Admin")))
        {
            error = Results.Json(
                new { message = "Доступ только для владельца или администратора." },
                statusCode: StatusCodes.Status403Forbidden);
            return false;
        }

        error = null;
        return true;
    }

    private static List<string> NormalizeCharacterIds(List<string>? characterIds) =>
        (characterIds ?? new List<string>())
            .Select(id => id?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(id => id.Length > 0 && id.Length <= 32)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public record CreateGroupChatRequest(string? Name, List<string>? CharacterIds);
public record SetParticipantsRequest(List<string>? CharacterIds);
public record SendGroupMessageRequest(string? Message);
