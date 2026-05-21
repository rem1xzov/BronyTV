using BronyTV.Models;

namespace BronyTV.Service;

public interface ISeasonService
{
    Task<IEnumerable<Season>> GetAllSeasonsAsync();
}