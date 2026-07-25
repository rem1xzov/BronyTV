using Microsoft.AspNetCore.Http;

namespace BronyTV.Infrastructure;

public static class AuthCookieHelper
{
    public const string SessionCookieName = "bronytv_session";

    public static CookieOptions CreateSessionCookieOptions(HttpRequest request, int lifetimeDays) =>
        new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(lifetimeDays),
            IsEssential = true
        };

    public static CookieOptions CreateExpiredCookieOptions(HttpRequest request) =>
        new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch,
            IsEssential = true
        };
}
