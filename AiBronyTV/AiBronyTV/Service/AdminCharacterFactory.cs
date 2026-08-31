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
            throw new ArgumentException($"Админский персонаж с ID '{characterId}' не найден!");
        }

        return persona + "\n\n" + AdminSystemPrompts.AdminUniversalRules;
    }
}
