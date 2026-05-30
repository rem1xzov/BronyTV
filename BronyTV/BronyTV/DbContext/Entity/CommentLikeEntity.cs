namespace BronyTV.DbContext.Entity;

public class CommentLikeEntity
{
    public Guid UserId { get; set; }
    public Guid CommentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public UserEntity User { get; set; } = null!;
    public CommentEntity Comment { get; set; } = null!;
}
