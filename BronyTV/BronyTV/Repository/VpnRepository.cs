using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class VpnRepository : IVpnRepository
{
    private readonly DbBronyTV _context;

    public VpnRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<bool> TrialUsedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.VpnSubscriptions
            .AsNoTracking()
            .AnyAsync(subscription => subscription.UserId == userId && subscription.Kind == "trial", cancellationToken);

    public async Task<VpnSubscriptionEntity?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.VpnSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.UserId == userId && !subscription.IsRevoked)
            .OrderByDescending(subscription => subscription.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<VpnSubscriptionEntity?> GetActiveTrackedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.VpnSubscriptions
            .Where(subscription => subscription.UserId == userId && !subscription.IsRevoked)
            .OrderByDescending(subscription => subscription.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VpnSubscriptionEntity>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _context.VpnSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.UserId == userId)
            .OrderByDescending(subscription => subscription.StartedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<VpnSubscriptionEntity> CreateSubscriptionAsync(
        VpnSubscriptionEntity subscription,
        CancellationToken cancellationToken = default)
    {
        _context.VpnSubscriptions.Add(subscription);
        await _context.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task UpdateSubscriptionAsync(
        VpnSubscriptionEntity subscription,
        CancellationToken cancellationToken = default)
    {
        _context.VpnSubscriptions.Update(subscription);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await _context.VpnSubscriptions
            .Where(subscription => subscription.UserId == userId && !subscription.IsRevoked)
            .ToListAsync(cancellationToken);
        if (active.Count == 0)
        {
            return false;
        }

        foreach (var subscription in active)
        {
            subscription.IsRevoked = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task CompletePromoActivationAsync(
        VpnSubscriptionEntity? newSubscription,
        VpnSubscriptionEntity? existingSubscription,
        VpnPromoKeyEntity promo,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (newSubscription != null)
            {
                _context.VpnSubscriptions.Add(newSubscription);
            }
            else if (existingSubscription != null)
            {
                _context.VpnSubscriptions.Update(existingSubscription);
            }

            _context.VpnPromoKeys.Update(promo);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<VpnPromoKeyEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await _context.VpnPromoKeys.FirstOrDefaultAsync(promo => promo.Code == code, cancellationToken);

    public async Task<VpnPromoKeyEntity> CreatePromoKeyAsync(
        VpnPromoKeyEntity key,
        CancellationToken cancellationToken = default)
    {
        _context.VpnPromoKeys.Add(key);
        await _context.SaveChangesAsync(cancellationToken);
        return key;
    }

    public async Task SavePromoKeyAsync(VpnPromoKeyEntity key, CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VpnPromoKeyEntity>> ListPromoKeysAsync(
        int limit,
        bool unusedOnly,
        CancellationToken cancellationToken = default)
    {
        IQueryable<VpnPromoKeyEntity> query = _context.VpnPromoKeys
            .AsNoTracking()
            .OrderByDescending(promo => promo.CreatedAtUtc);
        if (unusedOnly)
        {
            query = query.Where(promo => !promo.IsUsed);
        }

        return await query.Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<int> GetPromoKeysTotalAsync(CancellationToken cancellationToken = default) =>
        await _context.VpnPromoKeys.CountAsync(cancellationToken);

    public async Task<(IReadOnlyList<VpnSubscriptionEntity> Items, int Total)> ListSubscriptionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.VpnSubscriptions
            .AsNoTracking()
            .OrderByDescending(subscription => subscription.StartedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<ReferralRewardEntity> AddReferralRewardAsync(
        ReferralRewardEntity reward,
        CancellationToken cancellationToken = default)
    {
        _context.ReferralRewards.Add(reward);
        await _context.SaveChangesAsync(cancellationToken);
        return reward;
    }

    public async Task<IReadOnlyList<ReferralRewardEntity>> ListReferralRewardsAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        await _context.ReferralRewards
            .AsNoTracking()
            .OrderByDescending(reward => reward.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
