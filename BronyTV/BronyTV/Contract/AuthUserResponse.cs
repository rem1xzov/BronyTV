namespace BronyTV.Contract;

public class AuthUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? AvatarEmoji { get; set; }
    public string Race { get; set; } = string.Empty;
    public string PlatformRole { get; set; } = "User";
    public bool IsOwner { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public bool IsBannedFromCommenting { get; set; }
}
