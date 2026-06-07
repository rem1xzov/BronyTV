using BronyTV.DbContext.Entity;
using BronyTV.Models;
using Microsoft.Extensions.Options;

namespace BronyTV.Infrastructure;

public class AdminAccessService : IAdminAccessService
{
    private readonly HashSet<string> _privilegedUsernames;
    private readonly HashSet<string> _privilegedEmails;
    private readonly HashSet<string> _ownerUsernames;
    private readonly HashSet<Guid> _ownerUserIds;

    public AdminAccessService(IOptions<AdminAccessOptions> options)
    {
        var settings = options.Value;
        _privilegedUsernames = BuildSet(settings.PrivilegedUsernames);
        _privilegedEmails = BuildSet(settings.PrivilegedEmails);
        _ownerUsernames = BuildSet(settings.OwnerUsernames);
        _ownerUserIds = settings.OwnerUserIds?
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToHashSet() ?? new HashSet<Guid>();
    }

    public bool IsPrivilegedUser(string? username, string? email)
    {
        if (!string.IsNullOrWhiteSpace(username)
            && (_privilegedUsernames.Contains(username.Trim().ToLowerInvariant())
                || _ownerUsernames.Contains(username.Trim().ToLowerInvariant())))
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

    public bool IsOwnerUser(UserEntity user)
    {
        if (PlatformRoles.IsOwner(user.PlatformRole))
        {
            return true;
        }

        if (_ownerUserIds.Contains(user.Id))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(user.Username)
            && _ownerUsernames.Contains(user.Username.Trim().ToLowerInvariant()))
        {
            return true;
        }

        return false;
    }

    public bool IsProtectedOwner(UserEntity user) => IsOwnerUser(user);

    public string ResolveInitialRoleForUsername(string normalizedUsername) =>
        _ownerUsernames.Contains(normalizedUsername)
            ? PlatformRoles.Owner
            : PlatformRoles.User;

    private static HashSet<string> BuildSet(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>(StringComparer.Ordinal);
}
