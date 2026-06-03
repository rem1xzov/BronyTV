using BronyTV.Contract;

namespace BronyTV.Service;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserSummaryResponse>> SearchUsersAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, int StatusCode)> DeleteUserAsync(
        Guid userId,
        Guid actingAdminUserId,
        CancellationToken cancellationToken = default);

    Task<(AdminUserSummaryResponse? Response, string? Error, int StatusCode)> ToggleCommentBanAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
