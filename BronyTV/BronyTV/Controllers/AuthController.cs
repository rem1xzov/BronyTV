using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.Infrastructure;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IUserAuthService _userAuthService;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAdminService adminService,
        IUserAuthService userAuthService,
        IUserRepository userRepository,
        IConfiguration configuration)
    {
        _adminService = adminService;
        _userAuthService = userAuthService;
        _userRepository = userRepository;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AdminLogin([FromBody] LoginRequest request)
    {
        var token = await _adminService.LoginAsync(request.Username, request.Password);
        if (token == null)
        {
            return Unauthorized("Неверный логин или пароль");
        }

        return Ok(new { Token = token });
    }

    [HttpPost("register")]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
                var (response, error) = await _userAuthService.RegisterAsync(
            request.Email,
            request.Password,
            request.Race,
            request.Username,
            request.ReferralCode,
            cancellationToken);

        if (response == null)
        {
            return BadRequest(new { message = error ?? "Не удалось зарегистрироваться." });
        }

        // No session is issued until the six-digit code has been verified.
        return Accepted(response);
    }

    [HttpPost("signin")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SignIn([FromBody] UserLoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userAuthService.AuthenticateAsync(request.Email, request.Password, cancellationToken);
        if (user == null)
        {
            return Unauthorized(new { message = "Неверный email или пароль." });
        }

        if (!user.IsEmailConfirmed)
        {
            var (sent, resendError) = await _userAuthService.ResendEmailConfirmationAsync(
                user.Email,
                cancellationToken);

            return Conflict(new
            {
                message = sent
                    ? "Email ещё не подтверждён. Новый код отправлен на почту."
                    : resendError ?? "Email ещё не подтверждён. Введите ранее полученный код или запросите новый.",
                email = user.Email,
                requiresEmailConfirmation = true
            });
        }

        AppendSessionCookie(user);
        return Ok(_userAuthService.MapUserResponse(user));
    }

    [Authorize(Roles = "User")]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsEmailConfirmed)
        {
            return Unauthorized();
        }

        return Ok(_userAuthService.MapUserResponse(user));
    }

    [Authorize(Roles = "User")]
    [HttpPut("update-username")]
    public async Task<IActionResult> UpdateUsername(
        [FromBody] UpdateUsernameRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error) = await _userAuthService.UpdateUsernameAsync(
            userId,
            request.Username,
            cancellationToken);

        if (response == null)
        {
            return BadRequest(new { message = error ?? "Не удалось обновить юзернейм." });
        }

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpPut("update-password")]
    public async Task<IActionResult> UpdatePassword(
        [FromBody] UpdatePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (success, error) = await _userAuthService.UpdatePasswordAsync(
            userId,
            request.NewPassword,
            request.ConfirmPassword,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Не удалось изменить пароль." });
        }

        return Ok(new { message = "Пароль успешно изменён." });
    }

    [Authorize(Roles = "User")]
    [HttpPut("update-avatar-emoji")]
    public async Task<IActionResult> UpdateAvatarEmoji(
        [FromBody] UpdateAvatarEmojiRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error) = await _userAuthService.UpdateAvatarEmojiAsync(
            userId,
            request.Emoji,
            cancellationToken);

        if (response == null)
        {
            return BadRequest(new { message = error ?? "Не удалось обновить эмодзи." });
        }

        return Ok(response);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Append(
            AuthCookieHelper.SessionCookieName,
            string.Empty,
            AuthCookieHelper.CreateExpiredCookieOptions(Request));
        return Ok();
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var (success, error) = await _userAuthService.ConfirmEmailAsync(
            request.Email,
            request.Token,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Не удалось подтвердить email." });
        }

        // Confirmation succeeds, so the account may now be activated:
        // establish a session and return the full user payload.
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        AppendSessionCookie(user);
        return Ok(_userAuthService.MapUserResponse(user));
    }

    [HttpPost("resend-email-confirmation")]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> ResendEmailConfirmation(
        [FromBody] ResendEmailConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var (success, error) = await _userAuthService.ResendEmailConfirmationAsync(
            request.Email,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Не удалось отправить письмо." });
        }

        return Ok(new { message = "Письмо с подтверждением отправлено." });
    }

    private void AppendSessionCookie(DbContext.Entity.UserEntity user)
    {
        var sessionToken = _userAuthService.CreateSessionToken(user);
        var lifetimeDays = int.TryParse(_configuration["Jwt:SessionDays"], out var days) ? days : 7;
        Response.Cookies.Append(
            AuthCookieHelper.SessionCookieName,
            sessionToken,
            AuthCookieHelper.CreateSessionCookieOptions(Request, lifetimeDays));
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
