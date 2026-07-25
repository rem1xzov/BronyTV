using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface ISupportRepository
{
    Task<IReadOnlyList<SupportTicketEntity>> GetTicketsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportTicketEntity>> GetAllTicketsAsync(
        string? searchQuery,
        CancellationToken cancellationToken = default);

    Task<SupportTicketEntity?> GetTicketByIdAsync(
        Guid ticketId,
        bool includeMessages = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportMessageEntity>> GetMessagesByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketEntity> AddTicketAsync(
        SupportTicketEntity ticket,
        CancellationToken cancellationToken = default);

    Task<SupportMessageEntity> AddMessageAsync(
        SupportMessageEntity message,
        CancellationToken cancellationToken = default);

    Task<bool> CloseTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
