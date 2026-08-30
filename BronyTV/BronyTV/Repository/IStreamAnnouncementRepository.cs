using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IStreamAnnouncementRepository
{
    Task<IReadOnlyList<StreamAnnouncementEntity>> ListAsync(CancellationToken cancellationToken = default);
    Task<StreamAnnouncementEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StreamAnnouncementEntity> CreateAsync(StreamAnnouncementEntity announcement, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(Guid id, CancellationToken cancellationToken = default);
}
