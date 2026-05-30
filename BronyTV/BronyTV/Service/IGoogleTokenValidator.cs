namespace BronyTV.Service;

public sealed record GoogleTokenPayload(string Subject, string Email, string Name);

public interface IGoogleTokenValidator
{
    Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
