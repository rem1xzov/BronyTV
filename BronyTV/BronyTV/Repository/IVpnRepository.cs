using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IVpnRepository
{
    Task<bool> TrialUsedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<VpnSubscriptionEntity?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VpnSubscriptionEntity>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<VpnSubscriptionEntity> CreateSubscriptionAsync(
        VpnSubscriptionEntity subscription,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Атомарно сохраняет создание/продление подписки и пометку промо-кода использованным
    /// в одной транзакции.
    /// </summary>
    Task CompletePromoActivationAsync(
        VpnSubscriptionEntity? newSubscription,
        VpnSubscriptionEntity? existingSubscription,
        VpnPromoKeyEntity promo,
        CancellationToken cancellationToken = default);

    // --- Промо-ключи ---
    Task<VpnPromoKeyEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<VpnPromoKeyEntity> CreatePromoKeyAsync(VpnPromoKeyEntity key, CancellationToken cancellationToken = default);
    Task SavePromoKeyAsync(VpnPromoKeyEntity key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VpnPromoKeyEntity>> ListPromoKeysAsync(int limit, bool unusedOnly, CancellationToken cancellationToken = default);
    Task<int> GetPromoKeysTotalAsync(CancellationToken cancellationToken = default);

    // --- Подписки (админка) ---
    Task<(IReadOnlyList<VpnSubscriptionEntity> Items, int Total)> ListSubscriptionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    // --- Реферальные начисления ---
    Task<ReferralRewardEntity> AddReferralRewardAsync(
        ReferralRewardEntity reward,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferralRewardEntity>> ListReferralRewardsAsync(int limit, CancellationToken cancellationToken = default);
}
