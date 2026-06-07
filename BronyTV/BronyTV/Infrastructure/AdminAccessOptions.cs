namespace BronyTV.Infrastructure;

public class AdminAccessOptions
{
    public const string SectionName = "Admin";

    public string[] PrivilegedUsernames { get; set; } = ["rainbowdash"];

    public string[] PrivilegedEmails { get; set; } = [];

    public string[] OwnerUsernames { get; set; } = ["rainbowdash"];

    public string[] OwnerUserIds { get; set; } = [];
}
