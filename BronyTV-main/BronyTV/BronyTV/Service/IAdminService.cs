using BronyTV.Models;

namespace BronyTV.Service;

public interface IAdminService
{
    Task<string?> LoginAsync(string username, string password);
}