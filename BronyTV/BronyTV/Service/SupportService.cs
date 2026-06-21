using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Models;
using BronyTV.Repository;

namespace BronyTV.Service;

public class SupportService : ISupportService
{
    private const int MaxTitleLength = 150;
    private const int MaxContentLength = 4000;
    private readonly ISupportRepository _supportRepository;
    private readonly IUserRepository _userRepository;

    public SupportService(ISupportRepository supportRepository, IUserRepository userRepository)
    {
        _supportRepository = supportRepository;
        _userRepository = userRepository;
    }

    public async Task<(SupportTicketResponse? Response, string? Error, int StatusCode)> CreateTicketAsync(
        Guid userId,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status401Unauthorized);
        }

        var trimmedTitle = title.Trim();
        if (string.IsNullOrEmpty(trimmedTitle))
        {
            return (null, "Укажите тему обращения.", StatusCodes.Status400BadRequest);
        }

        if (trimmedTitle.Length > MaxTitleLength)
        {
            return (null, $"Тема не может быть длиннее {MaxTitleLength} символов.", StatusCodes.Status400BadRequest);
        }

        var trimmedContent = content.Trim();
        if (string.IsNullOrEmpty(trimmedContent))
        {
            return (null, "Опишите проблему.", StatusCodes.Status400BadRequest);
        }

        if (trimmedContent.Length > MaxContentLength)
        {
            return (null, $"Сообщение не может быть длиннее {MaxContentLength} символов.", StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        var ticketId = Guid.NewGuid();
        var message = new SupportMessageEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderId = userId,
            Content = trimmedContent,
            CreatedAtUtc = now
        };

        var ticket = new SupportTicketEntity
        {
            Id = ticketId,
            UserId = userId,
            Title = trimmedTitle,
            IsClosed = false,
            CreatedAtUtc = now,
            Messages = new List<SupportMessageEntity> { message }
        };

        message.Ticket = ticket;

        await _supportRepository.AddTicketAsync(ticket, cancellationToken);
        return (MapTicket(ticket), null, StatusCodes.Status200OK);
    }

    public async Task<IReadOnlyList<SupportTicketResponse>> GetMyTicketsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tickets = await _supportRepository.GetTicketsByUserIdAsync(userId, cancellationToken);
        return tickets.Select(MapTicket).ToList();
    }

    public async Task<(IReadOnlyList<SupportMessageResponse> Messages, string? Error, int StatusCode)> GetTicketMessagesAsync(
        Guid ticketId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _supportRepository.GetTicketByIdAsync(ticketId, cancellationToken: cancellationToken);
        if (ticket == null)
        {
            return (Array.Empty<SupportMessageResponse>(), "Обращение не найдено.", StatusCodes.Status404NotFound);
        }

        if (!isAdmin && ticket.UserId != userId)
        {
            return (Array.Empty<SupportMessageResponse>(), "Нет доступа к этому обращению.", StatusCodes.Status403Forbidden);
        }

        var messages = await _supportRepository.GetMessagesByTicketIdAsync(ticketId, cancellationToken);
        return (messages.Select(MapMessage).ToList(), null, StatusCodes.Status200OK);
    }

    public async Task<(SupportTicketResponse? Response, string? Error, int StatusCode)> AddMessageAsync(
        Guid ticketId,
        Guid senderId,
        bool isAdmin,
        string content,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _supportRepository.GetTicketByIdAsync(ticketId, includeMessages: true, cancellationToken);
        if (ticket == null)
        {
            return (null, "Обращение не найдено.", StatusCodes.Status404NotFound);
        }

        if (!isAdmin && ticket.UserId != senderId)
        {
            return (null, "Нет доступа к этому обращению.", StatusCodes.Status403Forbidden);
        }

        if (ticket.IsClosed)
        {
            return (null, "Обращение закрыто.", StatusCodes.Status400BadRequest);
        }

        var sender = await _userRepository.GetByIdAsync(senderId, cancellationToken);
        if (sender == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status401Unauthorized);
        }

        var trimmed = content.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (null, "Сообщение не может быть пустым.", StatusCodes.Status400BadRequest);
        }

        if (trimmed.Length > MaxContentLength)
        {
            return (null, $"Сообщение не может быть длиннее {MaxContentLength} символов.", StatusCodes.Status400BadRequest);
        }

        var message = new SupportMessageEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderId = senderId,
            Content = trimmed,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _supportRepository.AddMessageAsync(message, cancellationToken);
        ticket.Messages.Add(message);
        message.Sender = sender;

        return (MapTicket(ticket), null, StatusCodes.Status200OK);
    }

    public async Task<IReadOnlyList<SupportTicketResponse>> GetAllTicketsAsync(
        string? searchQuery,
        CancellationToken cancellationToken = default)
    {
        var tickets = await _supportRepository.GetAllTicketsAsync(searchQuery, cancellationToken);
        return tickets.Select(MapTicket).ToList();
    }

    public async Task<(bool Success, string? Error, int StatusCode)> CloseTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var closed = await _supportRepository.CloseTicketAsync(ticketId, cancellationToken);
        if (!closed)
        {
            return (false, "Обращение не найдено.", StatusCodes.Status404NotFound);
        }

        return (true, null, StatusCodes.Status200OK);
    }

    private static SupportTicketResponse MapTicket(SupportTicketEntity ticket)
    {
        var orderedMessages = ticket.Messages
            .OrderBy(message => message.CreatedAtUtc)
            .Select(MapMessage)
            .ToList();

        var firstMessage = orderedMessages.FirstOrDefault();
        var updatedAt = orderedMessages.Count > 0
            ? orderedMessages[^1].CreatedAt
            : ticket.CreatedAtUtc;

        return new SupportTicketResponse
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            Username = ticket.User?.Username ?? string.Empty,
            Title = ticket.Title,
            Subject = ticket.Title,
            Description = firstMessage?.Content ?? string.Empty,
            IsClosed = ticket.IsClosed,
            Status = ticket.IsClosed ? "closed" : "open",
            CreatedAt = ticket.CreatedAtUtc,
            UpdatedAt = updatedAt,
            Messages = orderedMessages
        };
    }

    private static SupportMessageResponse MapMessage(SupportMessageEntity message)
    {
        var isStaff = PlatformRoles.IsAdminOrOwner(message.Sender?.PlatformRole ?? PlatformRoles.User);

        return new SupportMessageResponse
        {
            Id = message.Id,
            TicketId = message.TicketId,
            SenderId = message.SenderId,
            Content = message.Content,
            Text = message.Content,
            AuthorRole = isStaff ? "admin" : "user",
            AuthorUsername = message.Sender?.Username ?? string.Empty,
            CreatedAt = message.CreatedAtUtc
        };
    }
}
