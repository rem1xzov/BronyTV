using BronyTV.Contract;
using BronyTV.DbContext.Entity;

namespace BronyTV.Service;

public interface IUserAuthService
{
    Task<(UserEntity User, bool IsNewUser)?> AuthenticateGoogleAsync(string idToken, CancellationToken cancellationToken = default);
    string CreateSessionToken(UserEntity user);
    AuthUserResponse MapUserResponse(UserEntity user);
    Task<AuthUserResponse?> SelectRaceAsync(Guid userId, string race, CancellationToken cancellationToken = default);
}
