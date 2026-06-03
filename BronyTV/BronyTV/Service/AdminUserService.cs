using BronyTV.Contract;
using BronyTV.Repository;

namespace BronyTV.Service;

public class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepository;

    public AdminUserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<AdminUserSummaryResponse>> SearchUsersAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.SearchByUsernameOrEmailAsync(query, cancellationToken);
        return users
            .Select(user => new AdminUserSummaryResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Race = user.Race,
                IsBannedFromCommenting = user.IsBannedFromCommenting,
                CreatedAtUtc = user.CreatedAtUtc
            })
            .ToList();
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

        user.IsBannedFromCommenting = !user.IsBannedFromCommenting;
        await _userRepository.SaveChangesAsync(user, cancellationToken);

        return (
            new AdminUserSummaryResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Race = user.Race,
                IsBannedFromCommenting = user.IsBannedFromCommenting,
                CreatedAtUtc = user.CreatedAtUtc
            },
            null,
            StatusCodes.Status200OK);
    }
}
