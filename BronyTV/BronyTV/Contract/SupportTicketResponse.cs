namespace BronyTV.Contract;

public class SupportTicketResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<SupportMessageResponse> Messages { get; set; } = Array.Empty<SupportMessageResponse>();
}
