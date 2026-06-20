namespace BronyTV.DbContext.Entity;

public class SupportMessageEntity
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public SupportTicketEntity Ticket { get; set; } = null!;
    public UserEntity Sender { get; set; } = null!;
}
