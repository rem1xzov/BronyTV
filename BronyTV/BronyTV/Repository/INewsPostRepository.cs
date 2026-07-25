using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface INewsPostRepository
{
    Task<IReadOnlyList<NewsPost>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<NewsPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NewsPost> AddAsync(NewsPost news, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
