namespace BronyTV.Contract;

public class AdminUserSummaryResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Race { get; set; } = string.Empty;
    public bool IsBannedFromCommenting { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
