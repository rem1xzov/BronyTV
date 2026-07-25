namespace BronyTV.Models;

public static class UserRace
{
    public const string Pegasus = "pegasus";
    public const string Unicorn = "unicorn";
    public const string EarthPony = "earth_pony";

    private static readonly HashSet<string> AllowedValues = new(StringComparer.Ordinal)
    {
        Pegasus,
        Unicorn,
        EarthPony
    };

    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var candidate = raw.Trim().ToLowerInvariant();
        if (!AllowedValues.Contains(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
