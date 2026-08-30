using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class StreamAnnouncementRepository : IStreamAnnouncementRepository
{
    private readonly DbBronyTV _context;

    public StreamAnnouncementRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StreamAnnouncementEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.StreamAnnouncements
            .AsNoTracking()
            .Include(a => a.Video)
                .ThenInclude(v => v.Season)
            .OrderByDescending(a => a.ScheduledAtUtc)
            .ToListAsync(cancellationToken);

    public Task<StreamAnnouncementEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.StreamAnnouncements
            .AsNoTracking()
            .Include(a => a.Video)
                .ThenInclude(v => v.Season)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<StreamAnnouncementEntity> CreateAsync(
        StreamAnnouncementEntity announcement,
        CancellationToken cancellationToken = default)
    {
        _context.StreamAnnouncements.Add(announcement);
        await _context.SaveChangesAsync(cancellationToken);
        return announcement;
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var announcement = await _context.StreamAnnouncements.FindAsync([id], cancellationToken);
        if (announcement == null)
        {
            return false;
        }

        announcement.Status = "cancelled";
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var announcement = await _context.StreamAnnouncements.FindAsync([id], cancellationToken);
        if (announcement != null)
        {
            announcement.Status = "completed";
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
