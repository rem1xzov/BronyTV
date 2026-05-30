using System.Text.RegularExpressions;

namespace BronyTV.Models;

public static partial class UsernameRules
{
    public const int MinLength = 4;
    public const int MaxLength = 25;

    [GeneratedRegex("^[a-zA-Z0-9_]{4,25}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    public static bool TryNormalize(string? raw, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Укажите юзернейм.";
            return false;
        }

        var candidate = raw.Trim();
        if (!UsernamePattern().IsMatch(candidate))
        {
            error = "Юзернейм: 4–25 символов, только латиница, цифры и подчёркивание.";
            return false;
        }

        normalized = candidate.ToLowerInvariant();
        return true;
    }
}
