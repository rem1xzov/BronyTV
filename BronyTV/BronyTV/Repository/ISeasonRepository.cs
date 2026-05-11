using BronyTV.Models;

namespace BronyTV.Repository;

public interface ISeasonRepository
{
    Task<IEnumerable<Season>> GetAllSeasonsAsync();
    Task<Season?> GetByNumberAsync(int number);
}