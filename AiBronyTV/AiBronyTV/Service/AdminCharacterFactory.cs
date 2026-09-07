using RainbowDashAI.Core;

namespace AiBronyTV.Service;

/// <summary>
/// Фабрика админских системных промптов. Полностью отделена от публичной
/// <see cref="CharacterFactory"/> — использует <see cref="AdminSystemPrompts"/>,
/// никак не пересекаясь с публичными промптами.
/// </summary>
public class AdminCharacterFactory
{
    public static string GetAdminSystemPrompt(string characterId)
    {
        var key = characterId.ToLower();
        if (!AdminSystemPrompts.Personas.TryGetValue(key, out var persona))
        {
            // Новые персонажи (например, EG-версии) в админке стартуют с обычным
            // публичным промптом — без специальных админских инструкций. Владелец
            // сможет донастроить их админские промпты вручную.
            return CharacterFactory.GetSystemPrompt(characterId);
        }

        return persona + "\n\n" + AdminSystemPrompts.AdminUniversalRules;
    }
}
