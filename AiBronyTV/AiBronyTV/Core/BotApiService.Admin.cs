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

        var userMessage = new ChatMessageEntity
        {
            SessionId = sessionId,
            CharacterId = characterId,
            Role = "user",
            Content = userInput,
            Timestamp = DateTime.UtcNow,
            IsAdminChat = true
        };
        _db.ChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(cancellationToken);

        // Сообщаем фронтенду реальный Id сохранённого сообщения пользователя —
        // он нужен для редактирования последнего сообщения.
        yield return new BotChunk(string.Empty, IsLimit: false, UserMessageId: userMessage.Id);

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

        await foreach (var chunk in GenerateAndSaveAssistantAsync(
            sessionId,
            characterId,
            isAdminChat: true,
            chatHistory,
            cancellationToken))
        {
            yield return chunk;
        }
    }
}
