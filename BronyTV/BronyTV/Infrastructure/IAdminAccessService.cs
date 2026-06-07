using BronyTV.DbContext.Entity;

namespace BronyTV.Infrastructure;

public interface IAdminAccessService
{
    bool IsPrivilegedUser(string? username, string? email);

    bool IsOwnerUser(UserEntity user);

    bool IsProtectedOwner(UserEntity user);

    string ResolveInitialRoleForUsername(string normalizedUsername);
}
