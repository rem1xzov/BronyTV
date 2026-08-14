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
    private const int MessageLimit = 50;
    private static readonly TimeSpan LimitWindow = TimeSpan.FromHours(5);

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
        int currentMaxLimit = isPremiumActive ? 200 : 50;

        // Owner and Admin can chat forever without any keys or limits.
        // Never call the paid model after the limit has been reached (for everyone else).
        if (!isStaff && limitEntry.Count >= currentMaxLimit)
        {
            // Do NOT save the user's message (do not spend their limit).
            // Instead, have the AI generate an in-character "limit reached" reply.
            var limitChatHistory = new ChatHistory(BuildSystemPrompt(characterId, userName, role));
            limitChatHistory.AddSystemMessage(
                "ИНСТРУКЦИЯ СИСТЕМЫ: Пользователь исчерпал лимит сообщений. " +
                $"Текущий лимит: {currentMaxLimit} сообщений. " +
                "Ответь ему строго в своём характере, что тебе нужен перерыв/ты устал(а). " +
                "ОБЯЗАТЕЛЬНО дай ссылку на Boosty: https://boosty.to/bronytvru и скажи, " +
                "что премиум-ключ оттуда снимет ограничения.");

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

        _db.ChatMessages.Add(new ChatMessageEntity
        {
            SessionId = sessionId,
            CharacterId = characterId,
            Role = "user",
            Content = userInput,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        var historyFromDb = await _db.ChatMessages
            .Where(message => message.SessionId == sessionId && message.CharacterId == characterId)
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
        var fullResponse = new StringBuilder();

        await foreach (var chunk in responseStream.WithCancellation(cancellationToken))
        {
            if (chunk.Content == null)
            {
                continue;
            }

            fullResponse.Append(chunk.Content);
            yield return new BotChunk(chunk.Content, IsLimit: false);
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
            Timestamp = DateTime.UtcNow
        });
                // Only count messages for non-staff users (Owner/Admin are unlimited).
        if (!isStaff)
        {
            limitEntry.Count++;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
