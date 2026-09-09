using System.Runtime.CompilerServices;
using System.Text;
using AiBronyTV.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiBronyTV.Core;

public partial class BotApiService
{
    /// <summary>
    /// Отправка сообщения в групповой чат. Каждый бот-участник отвечает по очереди
    /// (последовательно, а не параллельно), видя в контексте реплики предыдущих ботов.
    /// Каждый ответ бота — это 1 запрос к модели и списывает 1 сообщение из лимита
    /// пользователя (для Owner/Admin лимит безлимитный).
    /// </summary>
    public async IAsyncEnumerable<BotChunk> SendGroupMessageStreamAsync(
        int groupChatId,
        string userId,
        string userInput,
        bool isAdminChat,
        string? userName = null,
        string? role = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            yield break;
        }

        var group = await _db.GroupChats
            .FirstOrDefaultAsync(g => g.Id == groupChatId && g.UserId == userId && g.IsAdminChat == isAdminChat, cancellationToken);
        if (group == null)
        {
            throw new InvalidOperationException("Групповой чат не найден.");
        }

        var participants = await _db.GroupChatParticipants
            .Where(p => p.GroupChatId == groupChatId)
            .OrderBy(p => p.AddedAt)
            .ThenBy(p => p.CharacterId)
            .ToListAsync(cancellationToken);

        // Лимит привязан к аккаунту (userId из JWT).
        var nowUtc = DateTime.UtcNow;
        var limitEntry = await _db.UserLimits
            .FirstOrDefaultAsync(item => item.SessionId == userId, cancellationToken);
        if (limitEntry == null)
        {
            limitEntry = new UserLimitEntity { SessionId = userId, Date = nowUtc, Count = 0 };
            _db.UserLimits.Add(limitEntry);
        }
        else if (nowUtc - limitEntry.Date >= LimitWindow)
        {
            limitEntry.Date = nowUtc;
            limitEntry.Count = 0;
        }

        var roleKey = role?.Trim() ?? string.Empty;
        var isStaff = roleKey.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                      || roleKey.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        var isPremiumActive = limitEntry.PremiumUntil.HasValue && limitEntry.PremiumUntil.Value > DateTime.UtcNow;
        var currentMaxLimit = isPremiumActive ? PremiumMessageLimit : FreeMessageLimit;

        // Сообщение пользователя сохраняем всегда (само сообщение пользователя — не запрос).
        var userMessage = new GroupChatMessageEntity
        {
            GroupChatId = groupChatId,
            SenderType = "user",
            Content = userInput,
            CreatedAt = DateTime.UtcNow
        };
        _db.GroupChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(cancellationToken);

        yield return new BotChunk(string.Empty, IsLimit: false, UserMessageId: userMessage.Id);

        var limitReachedSignaled = false;

        foreach (var participant in participants)
        {
            // Если лимит исчерпан — дальше боты не отвечают, шлём одно уведомление.
            if (!isStaff && limitEntry.Count >= currentMaxLimit)
            {
                if (!limitReachedSignaled)
                {
                    yield return new BotChunk(string.Empty, IsLimit: true);
                    limitReachedSignaled = true;
                }
                break;
            }

            // Свежая история группового чата: сюда уже входят сообщение пользователя и
            // ответы предыдущих ботов этого же раунда (они сохранены в БД выше).
            var history = await _db.GroupChatMessages
                .Where(m => m.GroupChatId == groupChatId)
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .ToListAsync(cancellationToken);

            var systemPrompt = isAdminChat
                ? AdminCharacterFactory.GetAdminSystemPrompt(participant.CharacterId)
                : BuildSystemPrompt(participant.CharacterId, userName, role);

            var chatHistory = BuildGroupChatHistory(systemPrompt, history, participant.CharacterId);

            yield return new BotChunk(
                string.Empty,
                IsLimit: false,
                CharacterId: participant.CharacterId,
                Event: "bot_start");

            var fullResponse = new StringBuilder();
            await foreach (var chunk in StreamModelResponseAsync(chatHistory, cancellationToken))
            {
                fullResponse.Append(chunk.Text);
                yield return new BotChunk(chunk.Text, IsLimit: false, CharacterId: participant.CharacterId);
            }

            if (fullResponse.Length > 0)
            {
                _db.GroupChatMessages.Add(new GroupChatMessageEntity
                {
                    GroupChatId = groupChatId,
                    SenderType = "bot",
                    SenderCharacterId = participant.CharacterId,
                    Content = fullResponse.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);

                if (!isStaff)
                {
                    limitEntry.Count++;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            yield return new BotChunk(
                string.Empty,
                IsLimit: false,
                CharacterId: participant.CharacterId,
                Event: "bot_end");
        }
    }

    /// <summary>
    /// Строит историю группового чата для конкретного бота так, чтобы модель различала:
    /// - сообщения пользователя — как обычные user-реплики (без префикса);
    /// - собственные прошлые реплики бота — как assistant-реплики;
    /// - реплики ДРУГИХ ботов — как user-реплики с явным именем персонажа («[Имя]: ...»).
    /// </summary>
    private static ChatHistory BuildGroupChatHistory(
        string systemPrompt,
        IReadOnlyList<GroupChatMessageEntity> messages,
        string currentCharacterId)
    {
        var history = new ChatHistory(systemPrompt);

        foreach (var message in messages)
        {
            if (message.SenderType == "user")
            {
                history.AddUserMessage(message.Content);
            }
            else if (message.SenderType == "bot")
            {
                if (string.Equals(message.SenderCharacterId, currentCharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    history.AddAssistantMessage(message.Content);
                }
                else
                {
                    var name = BotCatalog.GetName(message.SenderCharacterId ?? string.Empty);
                    history.AddUserMessage($"[{name}]: {message.Content}");
                }
            }
        }

        return history;
    }
}
