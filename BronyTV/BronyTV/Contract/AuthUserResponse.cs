namespace BronyTV.Contract;

public class AuthUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Race { get; set; } = string.Empty;
}
