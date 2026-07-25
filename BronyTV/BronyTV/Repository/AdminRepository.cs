using BronyTV.DbContext;
using BronyTV.Models;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class AdminRepository : IAdminRepository
{
    private readonly DbBronyTV _context;

    public AdminRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<Admin?> GetByLoginAsync(string login)
    {
        var a = await _context.Admins.FirstOrDefaultAsync(x => x.Login == login);
        if (a == null)
        {
            return null;
        }

        return new Admin
        {
            Id = a.Id,
            Login = a.Login,
            PasswordHash = a.PasswordHash
        };
    }
}