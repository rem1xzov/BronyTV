namespace BronyTV.DbContext.Entity;

public class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? AvatarEmoji { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime RaceSelectedAtUtc { get; set; }
}
