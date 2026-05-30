using Microsoft.Extensions.Options;

namespace BronyTV.Infrastructure;

public class AdminAccessService : IAdminAccessService
{
    private readonly HashSet<string> _privilegedUsernames;
    private readonly HashSet<string> _privilegedEmails;

    public AdminAccessService(IOptions<AdminAccessOptions> options)
    {
        var settings = options.Value;
        _privilegedUsernames = BuildSet(settings.PrivilegedUsernames);
        _privilegedEmails = BuildSet(settings.PrivilegedEmails);
    }

    public bool IsPrivilegedUser(string? username, string? email)
    {
        if (!string.IsNullOrWhiteSpace(username)
            && _privilegedUsernames.Contains(username.Trim().ToLowerInvariant()))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(email)
            && _privilegedEmails.Contains(email.Trim().ToLowerInvariant()))
        {
            return true;
        }

        return false;
    }

    private static HashSet<string> BuildSet(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>(StringComparer.Ordinal);
}
