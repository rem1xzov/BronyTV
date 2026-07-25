namespace BronyTV.Contract;

public class SupportMessageResponse
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = "user";
    public string AuthorUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
