namespace BronyTV.DbContext.Entity;

public class ForumThreadEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid AuthorId { get; set; }

    public UserEntity Author { get; set; } = null!;
    public ICollection<ForumPostEntity> Posts { get; set; } = new List<ForumPostEntity>();
}
