using Microsoft.AspNetCore.Http;

namespace BronyTV.Infrastructure;

public static class AuthCookieHelper
{
    public const string SessionCookieName = "bronytv_session";

    public static CookieOptions CreateSessionCookieOptions(IHostEnvironment environment, int lifetimeDays) =>
        new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromDays(lifetimeDays),
            IsEssential = true
        };

    public static CookieOptions CreateExpiredCookieOptions(IHostEnvironment environment) =>
        new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch,
            IsEssential = true
        };
}
