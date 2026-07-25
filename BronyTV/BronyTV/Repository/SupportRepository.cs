using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class SupportRepository : ISupportRepository
{
    private readonly DbBronyTV _context;

    public SupportRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SupportTicketEntity>> GetTicketsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _context.SupportTickets
            .AsNoTracking()
            .Include(ticket => ticket.User)
            .Include(ticket => ticket.Messages)
                .ThenInclude(message => message.Sender)
            .Where(ticket => ticket.UserId == userId)
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SupportTicketEntity>> GetAllTicketsAsync(
        string? searchQuery,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SupportTickets
            .AsNoTracking()
            .Include(ticket => ticket.User)
            .Include(ticket => ticket.Messages)
                .ThenInclude(message => message.Sender)
            .Where(ticket => !ticket.IsClosed)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var normalized = searchQuery.Trim().ToLower();
            query = query.Where(ticket =>
                ticket.Title.ToLower().Contains(normalized)
                || (ticket.User.Username != null && ticket.User.Username.ToLower().Contains(normalized))
                || ticket.Messages.Any(message => message.Content.ToLower().Contains(normalized)));
        }

        return await query
            .OrderByDescending(ticket =>
                ticket.Messages.Count > 0
                    ? ticket.Messages.Max(message => message.CreatedAtUtc)
                    : ticket.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupportTicketEntity?> GetTicketByIdAsync(
        Guid ticketId,
        bool includeMessages = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SupportTicketEntity> query = _context.SupportTickets.AsNoTracking();

        if (includeMessages)
        {
            query = query
                .Include(ticket => ticket.User)
                .Include(ticket => ticket.Messages)
                    .ThenInclude(message => message.Sender);
        }
        else
        {
            query = query.Include(ticket => ticket.User);
        }

        return await query.FirstOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
    }

    public async Task<IReadOnlyList<SupportMessageEntity>> GetMessagesByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default) =>
        await _context.SupportMessages
            .AsNoTracking()
            .Include(message => message.Sender)
            .Where(message => message.TicketId == ticketId)
            .OrderBy(message => message.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<SupportTicketEntity> AddTicketAsync(
        SupportTicketEntity ticket,
        CancellationToken cancellationToken = default)
    {
        _context.SupportTickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<SupportMessageEntity> AddMessageAsync(
        SupportMessageEntity message,
        CancellationToken cancellationToken = default)
    {
        _context.SupportMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<bool> CloseTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken);

        if (ticket == null)
        {
            return false;
        }

        if (!ticket.IsClosed)
        {
            ticket.IsClosed = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
