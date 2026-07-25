namespace BronyTV.Contract;

public class PatchUserRequest
{
    public string? Role { get; set; }
    public bool? IsBannedFromCommenting { get; set; }
}
