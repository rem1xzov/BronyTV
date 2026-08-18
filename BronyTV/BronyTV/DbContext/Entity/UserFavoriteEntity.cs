namespace BronyTV.DbContext.Entity;

/// <summary>
/// Закладка «Избранное»: связка пользователя и видео/серии.
/// Один пользователь может отметить одну и ту же серию только один раз.
/// </summary>
public class UserFavoriteEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid VideoId { get; set; }

    public UserEntity? User { get; set; }
    public VideoEntity? Video { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
