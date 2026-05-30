using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.Infrastructure;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IUserAuthService _userAuthService;
    private readonly IUserRepository _userRepository;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAdminService adminService,
        IUserAuthService userAuthService,
        IUserRepository userRepository,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _adminService = adminService;
        _userAuthService = userAuthService;
        _userRepository = userRepository;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await _adminService.LoginAsync(request.Username, request.Password);
        if (token == null)
        {
            return Unauthorized("Неверный логин или пароль");
        }

        return Ok(new { Token = token });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _userAuthService.AuthenticateGoogleAsync(request.IdToken, cancellationToken);
        if (authResult == null)
        {
            return Unauthorized("Недействительный Google ID token.");
        }

        var (user, _) = authResult.Value;
        var sessionToken = _userAuthService.CreateSessionToken(user);
        var lifetimeDays = int.TryParse(_configuration["Jwt:SessionDays"], out var days) ? days : 7;
        Response.Cookies.Append(
            AuthCookieHelper.SessionCookieName,
            sessionToken,
            AuthCookieHelper.CreateSessionCookieOptions(_environment, lifetimeDays));

        return Ok(_userAuthService.MapUserResponse(user));
    }

    [Authorize(Roles = "User")]
    [HttpPost("select-race")]
    public async Task<IActionResult> SelectRace([FromBody] SelectRaceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var response = await _userAuthService.SelectRaceAsync(userId, request.Race, cancellationToken);
        if (response == null)
        {
            return Conflict("Раса уже выбрана или указано недопустимое значение.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Unauthorized();
        }

        var sessionToken = _userAuthService.CreateSessionToken(user);
        var lifetimeDays = int.TryParse(_configuration["Jwt:SessionDays"], out var days) ? days : 7;
        Response.Cookies.Append(
            AuthCookieHelper.SessionCookieName,
            sessionToken,
            AuthCookieHelper.CreateSessionCookieOptions(_environment, lifetimeDays));

        return Ok(response);
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
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(_userAuthService.MapUserResponse(user));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Append(
            AuthCookieHelper.SessionCookieName,
            string.Empty,
            AuthCookieHelper.CreateExpiredCookieOptions(_environment));
        return Ok();
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
