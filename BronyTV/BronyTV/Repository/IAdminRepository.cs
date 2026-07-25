using BronyTV.Models;

namespace BronyTV.Repository;

public interface IAdminRepository
{
    Task<Admin?> GetByLoginAsync(string username);
}