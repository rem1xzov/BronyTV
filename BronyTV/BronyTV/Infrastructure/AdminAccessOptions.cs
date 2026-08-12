namespace BronyTV.Infrastructure;

public class AdminAccessOptions
{
    public const string SectionName = "Admin";

    // Kept only for configuration compatibility. Usernames are not trusted for
    // privilege assignment because they are selected by users during registration.
    public string[] PrivilegedUsernames { get; set; } = [];

    public string[] PrivilegedEmails { get; set; } = [];

    public string[] OwnerUsernames { get; set; } = [];

    public string[] OwnerEmails { get; set; } = [];

    public string[] OwnerUserIds { get; set; } = [];
}
