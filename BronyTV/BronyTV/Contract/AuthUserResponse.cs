namespace BronyTV.Contract;

public class AuthUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Race { get; set; }
    public bool NeedsRaceSelection { get; set; }
}
