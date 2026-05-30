namespace BronyTV.Infrastructure;

public interface IAdminAccessService
{
    bool IsPrivilegedUser(string? username, string? email);
}
