using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AuthController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await _adminService.LoginAsync(request.Username, request.Password);

        if (token == null)
        {
            return Unauthorized("Неверный логин или пароль");
        }
        return Ok(new {Token = token});
    }
}