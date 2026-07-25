using BronyTV.Contract;

namespace BronyTV.Service;

public interface IAdminUserService
{
    Task<AdminUserListResponse> ListUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

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

    Task<(AdminUserSummaryResponse? Response, string? Error, int StatusCode)> PromoteToAdminAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(AdminUserSummaryResponse? Response, string? Error, int StatusCode)> DemoteFromAdminAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
