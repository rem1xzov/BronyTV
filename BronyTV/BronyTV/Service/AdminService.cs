using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BronyTV.Models;
using BronyTV.Repository;
using Microsoft.IdentityModel.Tokens;

namespace BronyTV.Service;

public class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepository;
    private readonly IConfiguration _configuration;

    public AdminService(IAdminRepository adminRepository, IConfiguration configuration)
    {
        _adminRepository = adminRepository;
        _configuration = configuration;
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        var admin = await _adminRepository.GetByLoginAsync(username);
        if (admin == null)
        {
            return null;
        }

        bool isValid = BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);
        if (!isValid)
        {
            return null;
        }
        
        return GenerateJwtToken(admin);
    }

    private string GenerateJwtToken(Admin admin)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, admin.Login),
            new Claim(ClaimTypes.Role, "Admin")
        };
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}