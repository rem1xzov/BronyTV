using BronyTV.Contract;

namespace BronyTV.Service;

public interface ISupportService
{
    Task<(SupportTicketResponse? Response, string? Error, int StatusCode)> CreateTicketAsync(
        Guid userId,
        string title,
        string content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportTicketResponse>> GetMyTicketsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SupportMessageResponse> Messages, string? Error, int StatusCode)> GetTicketMessagesAsync(
        Guid ticketId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<(SupportTicketResponse? Response, string? Error, int StatusCode)> AddMessageAsync(
        Guid ticketId,
        Guid senderId,
        bool isAdmin,
        string content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportTicketResponse>> GetAllTicketsAsync(
        string? searchQuery,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, int StatusCode)> CloseTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);
}
