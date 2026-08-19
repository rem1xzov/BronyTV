using System;
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

public interface IVpnService
{
    Task<VpnStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, VpnTrialStartResponse? Response)> StartTrialAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, VpnPromoActivateResponse? Response)> ActivatePromoCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class VpnService : IVpnService
{
    private readonly IVpnRepository _vpnRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<VpnOptions> _optionsAccessor;

    public VpnService(
        IVpnRepository vpnRepository,
        IUserRepository userRepository,
        IOptions<VpnOptions> options)
    {
        _vpnRepository = vpnRepository;
        _userRepository = userRepository;
        _optionsAccessor = options;
    }

    public async Task<VpnStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsAccessor.Value;
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        var active = await _vpnRepository.GetActiveAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;

        // Активна ли подписка с учётом срока действия.
        var isActive = active != null
            && !active.IsRevoked
            && (active.ExpiresAtUtc == null || active.ExpiresAtUtc > now);

        var status = new VpnStatusResponse
        {
            Enabled = options.Enabled,
            IsActive = isActive,
            IsTrialUsed = await _vpnRepository.TrialUsedAsync(userId, cancellationToken),
            ReferralBonusDays = options.TrialDays / 2 > 0 ? options.TrialDays / 2 : 3,
            ReferralCode = user?.ReferralCode
        };

        if (isActive && active != null)
        {
            status.PlanName = active.PlanName;
            status.ExpiresAtUtc = active.ExpiresAtUtc;

            if (active.ExpiresAtUtc.HasValue)
            {
                status.DaysLeft = Math.Max(0, (int)Math.Ceiling((active.ExpiresAtUtc.Value - now).TotalDays));
            }

            status.VlessLink = BuildVlessLink(active.ClientUuid ?? user?.Id.ToString(), options);
            status.PanelClientUrl = BuildPanelClientUrl(user?.Id.ToString(), options);
            status.ClientDownloadUrl = BuildClientDownloadUrl(options);
        }

        return status;
    }

    public async Task<(bool Success, string? Error, VpnTrialStartResponse? Response)> StartTrialAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsAccessor.Value;
        if (!options.Enabled)
        {
            return (false, "VPN-сервис временно недоступен.", null);
        }

        if (await _vpnRepository.TrialUsedAsync(userId, cancellationToken))
        {
            return (false, "Trial-подписка уже была использована.", null);
        }

        // Отключаем предыдущие активные подписки (например, если вдруг остались).
        await _vpnRepository.RevokeAsync(userId, cancellationToken);

        var subscription = new VpnSubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = "trial",
            PlanName = "BronyVPN Trial",
            StartedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(Math.Max(1, options.TrialDays)),
            ClientUuid = Guid.NewGuid().ToString(),
            PanelPlanNameId = "trial"
        };

        await _vpnRepository.CreateSubscriptionAsync(subscription, cancellationToken);

        return (true, null, new VpnTrialStartResponse
        {
            Success = true,
            ExpiresAtUtc = subscription.ExpiresAtUtc
        });
    }

    public async Task<(bool Success, string? Error, VpnPromoActivateResponse? Response)> ActivatePromoCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsAccessor.Value;
        if (!options.Enabled)
        {
            return (false, "VPN-сервис временно недоступен.", null);
        }

        var normalized = code?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (false, "Укажите промо-код.", null);
        }

        var promo = await _vpnRepository.GetByCodeAsync(normalized, cancellationToken);
        if (promo == null || promo.IsUsed)
        {
            return (false, "Неверный или уже использованный промо-код.", null);
        }

        // Расширяем текущую активную подписку; если её нет — создаём новую.
        var active = await _vpnRepository.GetActiveAsync(userId, cancellationToken);
        if (active == null || active.IsRevoked || (active.ExpiresAtUtc != null && active.ExpiresAtUtc <= DateTime.UtcNow))
        {
            var subscription = new VpnSubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = "promo",
                PlanName = "BronyVPN (промо)",
                StartedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                ClientUuid = promo.ClientUuid ?? Guid.NewGuid().ToString(),
                PanelPlanNameId = "1-month"
            };
            await _vpnRepository.CreateSubscriptionAsync(subscription, cancellationToken);
            promo.SubscriptionId = subscription.Id;
        }
        else
        {
            // Продлеваем действующую подписку с базовой точки.
            var baseTime = active.ExpiresAtUtc ?? DateTime.UtcNow;
            if (baseTime < DateTime.UtcNow)
            {
                baseTime = DateTime.UtcNow;
            }
            active.ExpiresAtUtc = baseTime.AddDays(30);
        }

        promo.IsUsed = true;
        promo.UsedAtUtc = DateTime.UtcNow;
        promo.UsedByUserId = userId;
        await _vpnRepository.SavePromoKeyAsync(promo, cancellationToken);

        var result = await GetStatusAsync(userId, cancellationToken);
        return (true, null, new VpnPromoActivateResponse
        {
            Success = true,
            PlanName = result.PlanName,
            ExpiresAtUtc = result.ExpiresAtUtc
        });
    }

    public async Task RevokeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _vpnRepository.RevokeAsync(userId, cancellationToken);
    }

    private string BuildVlessLink(string? remoteUuid, VpnOptions options)
    {
        var host = string.IsNullOrWhiteSpace(options.ServerHost) ? "vpn.bronytv.ru" : options.ServerHost;
        var uuid = string.IsNullOrWhiteSpace(remoteUuid) ? Guid.NewGuid().ToString() : remoteUuid;
        return VlessLinkBuilder.Build(
            uuid: uuid,
            host: host,
            port: options.ServerPort,
            parameters: options.VlessParameters,
            remark: "BronyVPN");
    }

    private string? BuildPanelClientUrl(string? clientId, VpnOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PanelBaseUrl))
        {
            return null;
        }
        var baseUrl = options.PanelBaseUrl.TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(options.PanelPath) ? "/" : options.PanelPath.TrimStart('/');
        return $"{baseUrl}/{path}{(string.IsNullOrWhiteSpace(clientId) ? "" : $"#/client/{clientId}")}";
    }

    private string? BuildClientDownloadUrl(VpnOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientDomain))
        {
            return null;
        }
        return $"https://{options.ClientDomain.Trim('/')}/";
    }
}
