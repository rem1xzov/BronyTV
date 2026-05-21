using BronyTV.DbContext;
using BronyTV.Models;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class SeasonRepository : ISeasonRepository
{
    private readonly DbBronyTV _context;

    public SeasonRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Season>> GetAllSeasonsAsync()
    {
        var entities = await _context.Seasons.OrderBy(s => s.Number).ToListAsync();

        return entities.Select(s => new Season
        {
            Id = s.Id,
            Number = s.Number,
            Title = s.Title,
            Description = s.Description,
            PosterPath = s.PosterPath
        });
    }

    public async Task<Season?> GetByNumberAsync(int number)
    {
        var s = await _context.Seasons.FirstOrDefaultAsync(x => x.Number == number);
        return s == null
            ? null
            : new Season
            {
                Id = s.Id,
                Number = s.Number,
                Title = s.Title,
            };
    }
}