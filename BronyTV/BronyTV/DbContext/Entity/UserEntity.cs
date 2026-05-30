namespace BronyTV.DbContext.Entity;

public class UserEntity
{
    public Guid Id { get; set; }
    public string GoogleSub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Race { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RaceSelectedAtUtc { get; set; }
}
