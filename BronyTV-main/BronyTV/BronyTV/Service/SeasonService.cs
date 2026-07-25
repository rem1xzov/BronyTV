using BronyTV.Models;
using BronyTV.Repository;

namespace BronyTV.Service;

public class SeasonService : ISeasonService
{
    private readonly ISeasonRepository _repository;

    public SeasonService(ISeasonRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Season>> GetAllSeasonsAsync() => await _repository.GetAllSeasonsAsync();
}