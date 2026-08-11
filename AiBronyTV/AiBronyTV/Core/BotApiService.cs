using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AiBronyTV.Service;

namespace AiBronyTV.Core;

public class BotApiService
{
    private readonly Kernel _kernel;
    private readonly AppDbContext _db;

    public BotApiService(Kernel kernel, AppDbContext db)
    {
        _kernel = kernel;
        _db = db;
    }

        public async IAsyncEnumerable<BotChunk> SendMessageStreamAsync(
        string sessionId, 
        string characterId, 
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            yield break;

        var today = DateTime.UtcNow.Date;
        
        // 1. Проверяем лимиты
        var limitEntry = await _db.UserLimits.FirstOrDefaultAsync(u => u.SessionId == sessionId, cancellationToken);
        
        if (limitEntry == null)
        {
            limitEntry = new UserLimitEntity { SessionId = sessionId, Date = today, Count = 0 };
            _db.UserLimits.Add(limitEntry);
        }
        else if (limitEntry.Date != today)
        {
            limitEntry.Date = today;
            limitEntry.Count = 0;
        }

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.7, 
            MaxTokens = 500,
            FrequencyPenalty = 0.5,
            PresencePenalty = 0.5
        };

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

                // 2. Блок лимитов: просим ИИ послать юзера на Boosty в своем стиле
        if (limitEntry.Count >= 50)
        {
            var boostyHistory = new ChatHistory(CharacterFactory.GetSystemPrompt(characterId));
            boostyHistory.AddUserMessage(
                "СИСТЕМНОЕ СООБЩЕНИЕ (ИГНОРИРУЙ ПРОШЛЫЙ ДИАЛОГ): " +
                "У пользователя закончился дневной лимит в 50 сообщений. " +
                "Скажи ему В СВОЕМ СТИЛЕ, что на сегодня лимит исчерпан. " +
                "Обязательно скажи, что если он хочет общаться дальше (до 100 сообщений в день), " +
                "ему нужно закинуть денег и оформить подписку на нашем Boosty. " +
                "Не отвечай на его прошлые вопросы, только продай подписку в своем характере.");

            var boostyStream = chatCompletion.GetStreamingChatMessageContentsAsync(boostyHistory, settings, _kernel, cancellationToken);
            var boostyResponse = new System.Text.StringBuilder();

            await foreach (var chunk in boostyStream)
            {
                if (chunk.Content != null)
                    boostyResponse.Append(chunk.Content);
            }

            if (boostyResponse.Length > 0)
            {
                // Emitted as a single chunk flagged as a limit reply so the frontend can style it.
                yield return new BotChunk(boostyResponse.ToString(), IsLimit: true);
            }

            yield break; // Останавливаем выполнение
        }

        // 3. Сохраняем запрос пользователя в БД
        _db.ChatMessages.Add(new ChatMessageEntity
        {
            SessionId = sessionId, 
            CharacterId = characterId, 
            Role = "user", 
            Content = userInput, 
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        // 4. Восстанавливаем историю: берем 20 ПОСЛЕДНИХ сообщений и разворачиваем их хронологически
        var historyFromDb = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId && m.CharacterId == characterId)
            .OrderByDescending(m => m.Timestamp)
            .Take(20)
            .ToListAsync(cancellationToken);
            
        historyFromDb.Reverse();

        var chatHistory = new ChatHistory(CharacterFactory.GetSystemPrompt(characterId));
        foreach (var msg in historyFromDb)
        {
            if (msg.Role == "user") chatHistory.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant") chatHistory.AddAssistantMessage(msg.Content);
        }

        // 5. Запускаем стриминг
        var responseStream = chatCompletion.GetStreamingChatMessageContentsAsync(chatHistory, settings, _kernel, cancellationToken);
        var fullResponse = new System.Text.StringBuilder();

                await foreach (var chunk in responseStream)
        {
            if (chunk.Content != null)
            {
                fullResponse.Append(chunk.Content);
                yield return new BotChunk(chunk.Content, IsLimit: false);
            }
        }

        // 6. Сохраняем ответ и обновляем лимит
        if (fullResponse.Length > 0)
        {
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
}