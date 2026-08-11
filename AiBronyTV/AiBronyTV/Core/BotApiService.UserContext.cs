using AiBronyTV.Service;

namespace AiBronyTV.Core;

public partial class BotApiService
{
    /// <summary>
    /// Формирует системный промпт с контекстом пользователя, чтобы бот знал, с кем общается.
    /// Если роль Admin/Owner — это Создатель/администратор сайта, бот общается уважительнее и чуть «ломает четвёртую стену».
    /// </summary>
    private static string BuildSystemPrompt(string characterId, string? userName, string? role)
    {
        var basePrompt = CharacterFactory.GetSystemPrompt(characterId);

        var userLabel = string.IsNullOrWhiteSpace(userName) ? "Гость" : userName.Trim();
        var roleLabel = string.IsNullOrWhiteSpace(role) ? "Пользователь сайта" : role.Trim();

        // Создатель сайта / администратор получают более тёплое, уважительное и чуть более неформальное общение.
        var ownerNote = "";
        if (roleLabel == "Owner")
        {
            ownerNote = "\nВажно: этот собеседник — Создатель самого сайта BronyTV. Общайся с ним особенно уважительно и дружелюбно, можешь ломать «четвёртую стену» и быть чуть более неформальным, но не выходя из своего характера.";
        }
        else if (roleLabel == "Admin")
        {
            ownerNote = "\nВажно: этот собеседник — администратор сайта BronyTV. Относись к нему с уважением, можешь быть чуть более открытым и неформальным, не выходя из своего характера.";
        }

        var context = $"Контекст пользователя: Тебя зовут {userLabel}. Твоя роль на этом сайте: {roleLabel}.{ownerNote}";

        return basePrompt + "\n\n" + context;
    }
}
