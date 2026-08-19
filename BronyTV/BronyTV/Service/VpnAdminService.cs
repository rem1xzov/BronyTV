using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Infrastructure;
using BronyTV.Models;
using BronyTV.Repository;
using Microsoft.Extensions.Options;

namespace BronyTV.Service;

public interface IVpnAdminService
{
    Task<string> GeneratePromoKeyAsync(CancellationToken cancellationToken = default);
    Task<VpnAdminPromoKeyListResponse> ListPromoKeysAsync(CancellationToken cancellationToken = default);
    Task<VpnAdminSubscriptionListResponse> ListSubscriptionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<VpnAdminReferralRewardListResponse> ListReferralRewardsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Административные операции раздела VPN: генерация промо-ключей, списки подписок
/// и реферальных начислений для выдачи бонусных дней.
/// </summary>
public class VpnAdminService : IVpnAdminService
{
    private readonly IVpnRepository _vpnRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<VpnOptions> _options;

    public VpnAdminService(
        IVpnRepository vpnRepository,
        IUserRepository userRepository,
        IOptions<VpnOptions> options)
    {
        _vpnRepository = vpnRepository;
        _userRepository = userRepository;
        _options = options;
    }

    public async Task<string> GeneratePromoKeyAsync(CancellationToken cancellationToken = default)
    {
        string code;
        do
        {
            code = VpnConfig.GeneratePromoCode();
        }
        while (await _vpnRepository.GetByCodeAsync(code, cancellationToken) != null);

        await _vpnRepository.CreatePromoKeyAsync(new VpnPromoKeyEntity
        {
            Code = code,
            IsUsed = false,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return code;
    }

    public async Task<VpnAdminPromoKeyListResponse> ListPromoKeysAsync(CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Max(1, _options.Value.AdminPromoPageSize);
        var keys = await _vpnRepository.ListPromoKeysAsync(pageSize, unusedOnly: true, cancellationToken);
        var total = await _vpnRepository.GetPromoKeysTotalAsync(cancellationToken);

        var items = new List<VpnAdminPromoKeyItem>();
        foreach (var key in keys)
        {
            string? usedByUsername = null;
            if (key.UsedByUserId.HasValue)
            {
                var usedBy = await _userRepository.GetByIdAsync(key.UsedByUserId.Value, cancellationToken);
                usedByUsername = usedBy?.Username ?? usedBy?.Email;
            }

            items.Add(new VpnAdminPromoKeyItem
            {
                Code = key.Code,
                IsUsed = key.IsUsed,
                CreatedAtUtc = key.CreatedAtUtc,
                UsedAtUtc = key.UsedAtUtc,
                UsedByUsername = usedByUsername
            });
        }

        return new VpnAdminPromoKeyListResponse
        {
            Items = items,
            Total = total,
            Unused = items.Count(item => !item.IsUsed)
        };
    }

    public async Task<VpnAdminSubscriptionListResponse> ListSubscriptionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _vpnRepository.ListSubscriptionsAsync(page, pageSize, cancellationToken);
        var hasMore = page * pageSize < total;

        var resultItems = new List<VpnAdminSubscriptionItem>();
        foreach (var subscription in items)
        {
            var user = await _userRepository.GetByIdAsync(subscription.UserId, cancellationToken);
            resultItems.Add(new VpnAdminSubscriptionItem
            {
                SubscriptionId = subscription.Id,
                UserId = subscription.UserId,
                Username = user?.Username,
                Email = user?.Email,
                Kind = subscription.Kind,
                PlanName = subscription.PlanName,
                ExpiresAtUtc = subscription.ExpiresAtUtc,
                IsRevoked = subscription.IsRevoked
            });
        }

        return new VpnAdminSubscriptionListResponse
        {
            Items = resultItems,
            Total = total,
            HasMore = hasMore
        };
    }

    public async Task<VpnAdminReferralRewardListResponse> ListReferralRewardsAsync(CancellationToken cancellationToken = default)
    {
        const int limit = 200;
        var rewards = await _vpnRepository.ListReferralRewardsAsync(limit, cancellationToken);

        var items = new List<VpnAdminReferralRewardItem>();
        foreach (var reward in rewards)
        {
            var referrer = await _userRepository.GetByIdAsync(reward.ReferrerId, cancellationToken);
            var referral = await _userRepository.GetByIdAsync(reward.ReferralUserId, cancellationToken);
            items.Add(new VpnAdminReferralRewardItem
            {
                ReferrerId = reward.ReferrerId,
                ReferrerUsername = referrer?.Username ?? referrer?.Email,
                ReferralUserId = reward.ReferralUserId,
                ReferralUsername = referral?.Username ?? referral?.Email,
                BonusDays = reward.BonusDays,
                Reason = reward.Reason,
                IsRedeemed = reward.IsRedeemed,
                CreatedAtUtc = reward.CreatedAtUtc
            });
        }

        return new VpnAdminReferralRewardListResponse
        {
            Items = items,
            Total = rewards.Count
        };
    }
}
