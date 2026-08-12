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
    private const int DailyMessageLimit = 50;

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

        var today = DateTime.UtcNow.Date;
        var limitEntry = await _db.UserLimits
            .FirstOrDefaultAsync(item => item.SessionId == limitKey, cancellationToken);

        if (limitEntry == null)
        {
            limitEntry = new UserLimitEntity { SessionId = limitKey, Date = today, Count = 0 };
            _db.UserLimits.Add(limitEntry);
        }
        else if (limitEntry.Date != today)
        {
            limitEntry.Date = today;
            limitEntry.Count = 0;
        }

        // Never call the paid model after the limit has been reached.
        if (limitEntry.Count >= DailyMessageLimit)
        {
            yield return new BotChunk(
                $"На сегодня лимит в {DailyMessageLimit} сообщений исчерпан. Возвращайся завтра, и мы продолжим общение!",
                IsLimit: true);
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
        limitEntry.Count++;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
