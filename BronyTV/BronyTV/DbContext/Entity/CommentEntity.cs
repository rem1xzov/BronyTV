namespace BronyTV.DbContext.Entity;

public class CommentEntity
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public Guid UserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public VideoEntity Video { get; set; } = null!;
    public UserEntity User { get; set; } = null!;
}
