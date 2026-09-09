using System.Runtime.CompilerServices;
using System.Text;
using AiBronyTV.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AiBronyTV.Core;

public partial class BotApiService
{
    // Лимиты сообщений на пользователя (в пределах 24-часового окна).
    private const int FreeMessageLimit = 25;
    private const int PremiumMessageLimit = 200;
    private static readonly TimeSpan LimitWindow = TimeSpan.FromHours(24);

    private readonly Kernel _kernel;
    private readonly AppDbContext _db;

    public BotApiService(Kernel kernel, AppDbContext db)
    {
        _kernel = kernel;
        _db = db;
    }

    public async IAsyncEnumerable<BotChunk> SendMessageStreamAsync(
        string sessionId,
        string limitKey,
        string characterId,
        string userInput,
        string? userName = null,
        string? role = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            yield break;
        }

        var nowUtc = DateTime.UtcNow;
        var limitEntry = await _db.UserLimits
            .FirstOrDefaultAsync(item => item.SessionId == limitKey, cancellationToken);

        if (limitEntry == null)
        {
            // `Date` column now stores the UTC timestamp of the start of the current
            // counting window instead of a calendar date.
            limitEntry = new UserLimitEntity { SessionId = limitKey, Date = nowUtc, Count = 0 };
            _db.UserLimits.Add(limitEntry);
        }
		else if (nowUtc - limitEntry.Date >= LimitWindow)
        {
            // A new 5-hour window has started: reset the counter.
            limitEntry.Date = nowUtc;
            limitEntry.Count = 0;
        }

		// Staff (Owner/Admin) get unlimited access; otherwise enforce premium/free limits.
        var roleKey = role?.Trim() ?? string.Empty;
        var isStaff = roleKey.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                      || roleKey.Equals("Admin", StringComparison.OrdinalIgnoreCase);

        // Premium users get a higher limit; otherwise it's the standard free limit.
        var isPremiumActive = limitEntry.PremiumUntil.HasValue && limitEntry.PremiumUntil.Value > DateTime.UtcNow;
        int currentMaxLimit = isPremiumActive ? PremiumMessageLimit : FreeMessageLimit;

        // Owner and Admin can chat forever without any keys or limits.
        // Never call the paid model after the limit has been reached (for everyone else).
        if (!isStaff && limitEntry.Count >= currentMaxLimit)
        {
            // Do NOT save the user's message (do not spend their limit).
            // Instead, have the AI generate an in-character "limit reached" reply.
            var limitChatHistory = new ChatHistory(BuildSystemPrompt(characterId, userName, role));
            limitChatHistory.AddSystemMessage(
                "ИНСТРУКЦИЯ СИСТЕМЫ: Пользователь исчерпал лимит сообщений. " +
                "Ответь ему строго в своём характере, что тебе нужен перерыв/ты устал(а). " +
                "ОБЯЗАТЕЛЬНО дай ссылку на Boosty: https://boosty.to/bronytvru и скажи, " +
                "что премиум-ключ оттуда даёт безлимит всего за 50 рублей в месяц.");

            var limitCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var limitStream = limitCompletion.GetStreamingChatMessageContentsAsync(
                limitChatHistory,
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.7,
                    MaxTokens = 300,
                    FrequencyPenalty = 0.5,
                    PresencePenalty = 0.5
                },
                _kernel,
                cancellationToken);

            await foreach (var chunk in limitStream.WithCancellation(cancellationToken))
            {
                if (chunk.Content == null)
                {
                    continue;
                }

                yield return new BotChunk(chunk.Content, IsLimit: true);
            }

            // Do not increment limitEntry.Count — the user's limit window stays as is.
            yield break;
        }

        var userMessage = new ChatMessageEntity
        {
            SessionId = sessionId,
            CharacterId = characterId,
            Role = "user",
            Content = userInput,
            Timestamp = DateTime.UtcNow
        };
        _db.ChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(cancellationToken);

        // Сообщаем фронтенду реальный Id сохранённого сообщения пользователя —
        // он нужен для редактирования последнего сообщения.
        yield return new BotChunk(string.Empty, IsLimit: false, UserMessageId: userMessage.Id);

        var historyFromDb = await _db.ChatMessages
            .Where(message => message.SessionId == sessionId && message.CharacterId == characterId && !message.IsAdminChat)
            .OrderByDescending(message => message.Timestamp)
            .Take(20)
            .ToListAsync(cancellationToken);
        historyFromDb.Reverse();

        var chatHistory = new ChatHistory(BuildSystemPrompt(characterId, userName, role));
        foreach (var message in historyFromDb)
        {
            if (message.Role == "user")
            {
                chatHistory.AddUserMessage(message.Content);
            }
            else if (message.Role == "assistant")
            {
                chatHistory.AddAssistantMessage(message.Content);
            }
        }

        await foreach (var chunk in GenerateAndSaveAssistantAsync(
            sessionId,
            characterId,
            isAdminChat: false,
            chatHistory,
            cancellationToken))
        {
            yield return chunk;
        }

		// Only count messages for non-staff users (Owner/Admin are unlimited).
        if (!isStaff)
        {
            limitEntry.Count++;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Редактирование последнего сообщения пользователя. Гарантирует, что сообщение —
    /// предпоследнее в чате, а последнее — ответ бота на него. Старый ответ удаляется,
    /// текст сообщения заменяется, и генерируется новый ответ бота. Лимит при этом не
    /// расходуется (это тот же запрос, но изменённый).
    /// </summary>
    public async IAsyncEnumerable<BotChunk> EditMessageStreamAsync(
        string sessionId,
        string characterId,
        int messageId,
        string newText,
        bool isAdminChat,
        string? userName = null,
        string? role = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newText))
        {
            yield break;
        }

        var messages = await _db.ChatMessages
            .Where(message => message.SessionId == sessionId
                              && message.CharacterId == characterId
                              && message.IsAdminChat == isAdminChat)
            .OrderBy(message => message.Timestamp)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);

        var target = messages.FirstOrDefault(message => message.Id == messageId);
        if (target == null || target.Role != "user")
        {
            throw new InvalidOperationException("Сообщение не найдено или это не сообщение пользователя.");
        }

        // Защита от редактирования произвольного старого сообщения:
        // редактируемое сообщение должно быть предпоследним в чате, а сразу за ним —
        // ровно одно сообщение-ответ бота (последнее). Если после него есть ещё
        // сообщения или ответа нет — редактировать нельзя.
        var targetIndex = messages.IndexOf(target);
        if (targetIndex < 0
            || targetIndex != messages.Count - 2
            || messages[^1].Role != "assistant")
        {
            throw new InvalidOperationException("Редактировать можно только последнее сообщение пользователя в чате.");
        }

        // Удаляем старый ответ бота и заменяем текст сообщения пользователя.
        _db.ChatMessages.Remove(messages[^1]);
        target.Content = newText;
        await _db.SaveChangesAsync(cancellationToken);

        var systemPrompt = isAdminChat
            ? AdminCharacterFactory.GetAdminSystemPrompt(characterId)
            : BuildSystemPrompt(characterId, userName, role);

        var chatHistory = new ChatHistory(systemPrompt);
        foreach (var message in messages.Take(targetIndex + 1))
        {
            if (message.Role == "user")
            {
                chatHistory.AddUserMessage(message.Content);
            }
            else if (message.Role == "assistant")
            {
                chatHistory.AddAssistantMessage(message.Content);
            }
        }

        await foreach (var chunk in GenerateAndSaveAssistantAsync(
            sessionId,
            characterId,
            isAdminChat,
            chatHistory,
            cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Генерирует ответ бота по готовой истории диалога, стримит его и сохраняет в БД.
    /// Общий код для обычной отправки и редактирования (не дублируем логику генерации).
    /// </summary>
    private async IAsyncEnumerable<BotChunk> GenerateAndSaveAssistantAsync(
        string sessionId,
        string characterId,
        bool isAdminChat,
        ChatHistory chatHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fullResponse = new StringBuilder();
        await foreach (var chunk in StreamModelResponseAsync(chatHistory, cancellationToken))
        {
            fullResponse.Append(chunk.Text);
            yield return chunk;
        }

        if (fullResponse.Length == 0)
        {
            yield break;
        }

        _db.ChatMessages.Add(new ChatMessageEntity
        {
            SessionId = sessionId,
            CharacterId = characterId,
            Role = "assistant",
            Content = fullResponse.ToString(),
            Timestamp = DateTime.UtcNow,
            IsAdminChat = isAdminChat
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Общий механизм вызова модели: стримит текст ответа по готовому ChatHistory.
    /// Ничего не сохраняет — сохранением занимается вызывающий код (одиночный или
    /// групповой чат).
    /// </summary>
    private async IAsyncEnumerable<BotChunk> StreamModelResponseAsync(
        ChatHistory chatHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.7,
            MaxTokens = 500,
            FrequencyPenalty = 0.5,
            PresencePenalty = 0.5
        };
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var responseStream = chatCompletion.GetStreamingChatMessageContentsAsync(
            chatHistory,
            settings,
            _kernel,
            cancellationToken);

        await foreach (var chunk in responseStream.WithCancellation(cancellationToken))
        {
            if (chunk.Content == null)
            {
                continue;
            }

            yield return new BotChunk(chunk.Content, IsLimit: false);
        }
    }
}
