using System.Globalization;

namespace BronyTV.Models;

public static class EmojiRules
{
    public const int MaxLength = 32;

    public static bool TryNormalize(string? raw, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Выберите эмодзи.";
            return false;
        }

        var trimmed = raw.Trim();
        var elements = EnumerateTextElements(trimmed);
        if (elements.Count != 1)
        {
            error = "Укажите ровно один эмодзи.";
            return false;
        }

        normalized = elements[0];
        return true;
    }

    private static List<string> EnumerateTextElements(string value)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }
}
