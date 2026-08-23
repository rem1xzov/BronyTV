using BronyTV.DbContext.Entity;
using BronyTV.Models;
using Microsoft.Extensions.Options;

namespace BronyTV.Infrastructure;

public class AdminAccessService : IAdminAccessService
{
    private readonly HashSet<string> _privilegedEmails;
    private readonly HashSet<string> _ownerEmails;
    private readonly HashSet<Guid> _ownerUserIds;

    public AdminAccessService(IOptions<AdminAccessOptions> options)
    {
        var settings = options.Value;
        _privilegedEmails = BuildSet(settings.PrivilegedEmails);
        _ownerEmails = BuildSet(settings.OwnerEmails);
        _ownerUserIds = settings.OwnerUserIds?
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToHashSet() ?? new HashSet<Guid>();
    }

    public bool IsPrivilegedUser(string? username, string? email)
    {
        // Usernames are deliberately ignored: they are public registration input and
        // must never be enough to obtain administrator privileges.
        return !string.IsNullOrWhiteSpace(email)
            && (_privilegedEmails.Contains(Normalize(email))
                || _ownerEmails.Contains(Normalize(email)));
    }

    public bool IsOwnerUser(UserEntity user)
    {
        if (PlatformRoles.IsOwner(user.PlatformRole) || _ownerUserIds.Contains(user.Id))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(user.Email)
            && _ownerEmails.Contains(Normalize(user.Email));
    }

    public bool IsAdminOrOwner(UserEntity user)
    {
        // Mirrors the role resolution used when a session is validated in Program.cs:
        // Owner, or a user whose stored PlatformRole is Admin, or a "privileged" email
        // that is lifted to Admin without being materialised in the DB.
        return IsOwnerUser(user)
            || PlatformRoles.IsAdminOrOwner(user.PlatformRole)
            || IsPrivilegedUser(user.Username, user.Email);
    }

    public bool IsProtectedOwner(UserEntity user) => IsOwnerUser(user);

    public string ResolveInitialRole(string normalizedEmail)
    {
        var email = Normalize(normalizedEmail);
        if (_ownerEmails.Contains(email))
        {
            return PlatformRoles.Owner;
        }

        return _privilegedEmails.Contains(email) ? PlatformRoles.Admin : PlatformRoles.User;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static HashSet<string> BuildSet(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>(StringComparer.Ordinal);
}
