namespace BronyTV.Contract;

public sealed class RegistrationPendingResponse
{
    public string Email { get; init; } = string.Empty;
    public string Message { get; init; } = "Код подтверждения отправлен на email.";
    public bool RequiresEmailConfirmation { get; init; } = true;
    public int CodeExpiresInSeconds { get; init; }
}
