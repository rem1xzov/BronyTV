namespace BronyTV.DbContext.Entity;

public class SupportTicketEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public UserEntity User { get; set; } = null!;
    public ICollection<SupportMessageEntity> Messages { get; set; } = new List<SupportMessageEntity>();
}
