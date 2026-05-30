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
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var (response, error) = await _userAuthService.RegisterAsync(
            request.Email,
            request.Password,
            request.Race,
            cancellationToken);

        if (response == null)
        {
            return BadRequest(new { message = error ?? "Не удалось зарегистрироваться." });
        }

        var user = await _userRepository.GetByEmailAsync(response.Email, cancellationToken);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        AppendSessionCookie(user);
        return Ok(response);
    }

    [HttpPost("signin")]
    public async Task<IActionResult> SignIn([FromBody] UserLoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userAuthService.AuthenticateAsync(request.Email, request.Password, cancellationToken);
        if (user == null)
        {
            return Unauthorized(new { message = "Неверный email или пароль." });
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
        if (user == null)
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

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Append(
            AuthCookieHelper.SessionCookieName,
            string.Empty,
            AuthCookieHelper.CreateExpiredCookieOptions(Request));
        return Ok();
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
