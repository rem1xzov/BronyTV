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
    /// <summary>
    /// Админский чат: отдельный системный промпт, без лимитов, БЕЗ инструментов (function calling),
    /// история изолирована от публичной флагом <c>IsAdminChat</c> и sessionId вида "admin:{userId}".
    /// </summary>
    public async IAsyncEnumerable<BotChunk> SendAdminMessageStreamAsync(
        string sessionId,
        string characterId,
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            yield break;
        }

        _db.ChatMessages.Add(new ChatMessageEntity
        {
            SessionId = sessionId,
            CharacterId = characterId,
            Role = "user",
            Content = userInput,
            Timestamp = DateTime.UtcNow,
            IsAdminChat = true
        });
        await _db.SaveChangesAsync(cancellationToken);

        var historyFromDb = await _db.ChatMessages
            .Where(message => message.SessionId == sessionId
                              && message.CharacterId == characterId
                              && message.IsAdminChat)
            .OrderByDescending(message => message.Timestamp)
            .Take(20)
            .ToListAsync(cancellationToken);
        historyFromDb.Reverse();

        // Админский промпт — полностью отдельный (AdminCharacterFactory).
        var chatHistory = new ChatHistory(AdminCharacterFactory.GetAdminSystemPrompt(characterId));
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

        // Обычный текстовый chat completion без function calling / tools.
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
            Timestamp = DateTime.UtcNow,
            IsAdminChat = true
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
