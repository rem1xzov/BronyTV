namespace BronyTV.Models;

public static class PlatformRoles
{
    public const string User = "User";
    public const string Admin = "Admin";
    public const string Owner = "Owner";

    public static bool IsAdminOrOwner(string? role) =>
        string.Equals(role, Admin, StringComparison.Ordinal)
        || string.Equals(role, Owner, StringComparison.Ordinal);

    public static bool IsOwner(string? role) =>
        string.Equals(role, Owner, StringComparison.Ordinal);
}
