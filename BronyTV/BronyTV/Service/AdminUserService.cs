using BronyTV.Contract;
using BronyTV.Infrastructure;
using BronyTV.Models;
using BronyTV.Repository;

namespace BronyTV.Service;

public class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IAdminAccessService _adminAccessService;

    public AdminUserService(IUserRepository userRepository, IAdminAccessService adminAccessService)
    {
        _userRepository = userRepository;
        _adminAccessService = adminAccessService;
    }

    public async Task<AdminUserListResponse> ListUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _userRepository.ListUsersAsync(page, pageSize, cancellationToken);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        return new AdminUserListResponse
        {
            Items = items.Select(MapUser).ToList(),
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            HasMore = safePage * safePageSize < totalCount
        };
    }

    public async Task<IReadOnlyList<AdminUserSummaryResponse>> SearchUsersAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.SearchByUsernameOrEmailAsync(query, cancellationToken);
        return users.Select(MapUser).ToList();
    }

    public async Task<(bool Success, string? Error, int StatusCode)> DeleteUserAsync(
        Guid userId,
        Guid actingAdminUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == actingAdminUserId)
        {
            return (false, "Нельзя удалить собственный аккаунт.", StatusCodes.Status400BadRequest);
        }

        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return (false, "Пользователь не найден.", StatusCodes.Status404NotFound);
        }

        if (_adminAccessService.IsProtectedOwner(user))
        {
            return (false, "Нельзя изменять владельца платформы.", StatusCodes.Status403Forbidden);
        }

        await _userRepository.DeleteAsync(user, cancellationToken);
        return (true, null, StatusCodes.Status204NoContent);
    }

    public async Task<(AdminUserSummaryResponse? Response, string? Error, int StatusCode)> ToggleCommentBanAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status404NotFound);
        }

        if (_adminAccessService.IsProtectedOwner(user))
        {
            return (null, "Нельзя изменять владельца платформы.", StatusCodes.Status403Forbidden);
        }

        user.IsBannedFromCommenting = !user.IsBannedFromCommenting;
        await _userRepository.SaveChangesAsync(user, cancellationToken);
        return (MapUser(user), null, StatusCodes.Status200OK);
    }

    public async Task<(AdminUserSummaryResponse? Response, string? Error, int StatusCode)> PromoteToAdminAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status404NotFound);
        }

        if (_adminAccessService.IsProtectedOwner(user))
        {
            return (null, "Нельзя изменять роль владельца платформы.", StatusCodes.Status403Forbidden);
        }

        if (PlatformRoles.IsAdminOrOwner(user.PlatformRole))
        {
            return (null, "Пользователь уже является администратором.", StatusCodes.Status400BadRequest);
        }

        user.PlatformRole = PlatformRoles.Admin;
        await _userRepository.SaveChangesAsync(user, cancellationToken);
        return (MapUser(user), null, StatusCodes.Status200OK);
    }

    public async Task<(AdminUserSummaryResponse? Response, string? Error, int StatusCode)> DemoteFromAdminAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status404NotFound);
        }

        if (_adminAccessService.IsProtectedOwner(user))
        {
            return (null, "Нельзя изменять роль владельца платформы.", StatusCodes.Status403Forbidden);
        }

        if (!PlatformRoles.IsAdminOrOwner(user.PlatformRole))
        {
            return (null, "Пользователь не является администратором.", StatusCodes.Status400BadRequest);
        }

        user.PlatformRole = PlatformRoles.User;
        await _userRepository.SaveChangesAsync(user, cancellationToken);
        return (MapUser(user), null, StatusCodes.Status200OK);
    }

    private AdminUserSummaryResponse MapUser(DbContext.Entity.UserEntity user)
    {
        var isOwner = _adminAccessService.IsOwnerUser(user);
        return new AdminUserSummaryResponse
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Race = user.Race,
            Role = isOwner ? PlatformRoles.Owner : user.PlatformRole,
            IsOwner = isOwner,
            IsBannedFromCommenting = user.IsBannedFromCommenting,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }
}
