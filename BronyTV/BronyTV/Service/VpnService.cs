using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Infrastructure;
using BronyTV.Models;
using BronyTV.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BronyTV.Service;

public interface IVpnService
{
    Task<VpnStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>Aктивация trial-подписки.</para>
    /// <para><b>ServerError</b> = true означает сбой внешнего VPN-провайдера (3X-UI),
    /// который должен транслироваться на фронт как HTTP 502/500, а не как 400.</para>
    /// </summary>
    Task<(bool Success, string? Error, VpnTrialStartResponse? Response, bool ServerError)> StartTrialAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>Aктивация промо-кода.</para>
    /// <para><b>ServerError</b> = true означает сбой внешнего VPN-провайдера (3X-UI),
    /// который должен транслироваться на фронт как HTTP 502/500, а не как 400.</para>
    /// </summary>
    Task<(bool Success, string? Error, VpnPromoActivateResponse? Response, bool ServerError)> ActivatePromoCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>Начисляет N дней BronyVPN (продлевает активную подписку или создаёт новую).</para>
    /// <para>Используется наградами за стрики. <b>ServerError</b> = true означает сбой внешнего
    /// VPN-провайдера (3X-UI) либо не сконфигурированную панель.</para>
    /// </summary>
    Task<(bool Success, string? Error, bool ServerError)> GrantDaysAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class VpnService : IVpnService
{
    private readonly IVpnRepository _vpnRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<VpnOptions> _optionsAccessor;
    private readonly VpnConfigResolver _vpnConfig;
    private readonly IVpn3xUiClient _panelClient;
    private readonly ILogger<VpnService> _logger;

    public VpnService(
        IVpnRepository vpnRepository,
        IUserRepository userRepository,
        IOptions<VpnOptions> options,
        VpnConfigResolver vpnConfig,
        IVpn3xUiClient panelClient,
        ILogger<VpnService> logger)
    {
        _vpnRepository = vpnRepository;
        _userRepository = userRepository;
        _optionsAccessor = options;
        _vpnConfig = vpnConfig;
        _panelClient = panelClient;
        _logger = logger;
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
            TrialDays = options.TrialDays,
            ReferralCode = user?.ReferralCode
        };

        if (isActive && active != null)
        {
            // Восстанавливаем клиента на панели 3X-UI, если он пропал (удалён вручную,
            // сбой синхронизации) — чтобы не отдавать «мёртвую» ссылку.
            await EnsurePanelClientAsync(active, cancellationToken);

            status.PlanName = active.PlanName;
            status.ExpiresAtUtc = active.ExpiresAtUtc;

            if (active.ExpiresAtUtc.HasValue)
            {
                status.DaysLeft = Math.Max(0, (int)(active.ExpiresAtUtc.Value - now).TotalDays);
            }

            status.VlessLink = BuildVlessLink(active.ClientUuid ?? user?.Id.ToString(), options);
            status.PanelClientUrl = BuildPanelClientUrl(user?.Id.ToString(), options);
            status.ClientDownloadUrl = BuildClientDownloadUrl(options);
        }

        return status;
    }

    /// <summary>
    /// Гарантирует наличие клиента на панели 3X-UI для активной подписки.
    /// Если интеграция с панелью не настроена — просто выходит (dev-режим).
    /// Если клиент отсутствует на панели — пересоздаёт его.
    /// </summary>
    private async Task EnsurePanelClientAsync(
        VpnSubscriptionEntity subscription,
        CancellationToken cancellationToken)
    {
        if (!_panelClient.IsConfigured || string.IsNullOrWhiteSpace(subscription.ClientUuid))
        {
            return;
        }

        try
        {
            var exists = await _panelClient.ClientExistsAsync(subscription.ClientUuid, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                return;
            }

            var expiresAt = subscription.ExpiresAtUtc
                ?? DateTime.UtcNow.AddDays(Math.Max(1, _optionsAccessor.Value.TrialDays));
            var email = BuildPanelEmail(subscription.UserId);

            _logger.LogInformation(
                "3X-UI: клиент {Uuid} отсутствует на панели, восстанавливаю (subscription {SubId}).",
                subscription.ClientUuid,
                subscription.Id);

            await _panelClient.UpsertClientAsync(
                subscription.ClientUuid,
                email,
                expiresAt,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Восстановление не критично для отображения статуса — логируем и продолжаем.
            _logger.LogWarning(ex, "3X-UI: не удалось восстановить клиента {Uuid}.", subscription.ClientUuid);
        }
    }

                    /// <summary>
    /// Проверяет, что интеграция с 3X-UI полностью сконфигурирована (включена и
    /// заданы URL API + Bearer-токен). Провижионирование обязательно: если панель
    /// не готова, пользователю не должна выдаваться VLESS-ссылка.
    /// </summary>
    private bool IsPanelReady()
    {
        var enabled = _optionsAccessor.Value.Enabled;
        var hasUrl = !string.IsNullOrWhiteSpace(_vpnConfig.PanelApiUrl);
        var hasToken = !string.IsNullOrWhiteSpace(_vpnConfig.PanelApiToken);

        if (!enabled || !hasUrl || !hasToken)
        {
            _logger.LogWarning(
                "3X-UI: панель не готова (Enabled={Enabled}, HasApiUrl={HasApiUrl}, HasApiToken={HasApiToken}).",
                enabled,
                hasUrl,
                hasToken);
        }

        return enabled && hasUrl && hasToken;
    }

    public async Task<(bool Success, string? Error, VpnTrialStartResponse? Response, bool ServerError)> StartTrialAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsAccessor.Value;
        if (!options.Enabled)
        {
            return (false, "VPN-сервис временно недоступен.", null, false);
        }

        if (await _vpnRepository.TrialUsedAsync(userId, cancellationToken))
        {
            return (false, "Trial-подписка уже была использована.", null, false);
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

                // ===== АТОМАРНОСТЬ =====
        // Сначала провижионируем клиента на панели 3X-UI. Только при подтверждении
        // от 3X-UI, что клиент создан, сохраняем запись в PostgreSQL и возвращаем
        // успех. Если панель не сконфигурирована, недоступна или ответила ошибкой —
        // НЕ выдаём ссылку и наружу уходит явный ServerError (HTTP 502). Это
        // исключает ситуацию, когда пользователь получает синтаксически валидную,
        // но «мёртвую» VLESS-ссылку с UUID, которого нет на инбаунде сервера.
        if (!IsPanelReady())
        {
            _logger.LogError(
                "3X-UI: панель не сконфигурирована (VPN_ENABLED=true, но отсутствует " +
                "VPN_PANEL_API_URL или VPN_PANEL_API_TOKEN). Trial для пользователя {UserId} отклонён.",
                userId);
            _vpnConfig.LogDiagnostics();
            return (false, "Не удалось активировать trial: VPN-провайдер не настроен.", null, true);
        }

        bool provisioned;
        try
        {
            provisioned = await _panelClient.UpsertClientAsync(
                subscription.ClientUuid,
                BuildPanelEmail(userId),
                subscription.ExpiresAtUtc.Value,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "3X-UI: сбой при создании trial-клиента для пользователя {UserId}.", userId);
            return (false, "Не удалось активировать trial: VPN-провайдер недоступен.", null, true);
        }

        if (!provisioned)
        {
            _logger.LogWarning(
                "3X-UI: не удалось создать trial-клиента (success=false) для пользователя {UserId}.",
                userId);
            return (false, "Не удалось активировать trial: ошибка VPN-провайдера.", null, true);
        }

        // 3X-UI успешно провижионировал клиента — теперь сохраняем в БД.
        await _vpnRepository.CreateSubscriptionAsync(subscription, cancellationToken);

        return (true, null, new VpnTrialStartResponse
        {
            Success = true,
            ExpiresAtUtc = subscription.ExpiresAtUtc
        }, false);
    }

    public async Task<(bool Success, string? Error, VpnPromoActivateResponse? Response, bool ServerError)> ActivatePromoCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsAccessor.Value;
        if (!options.Enabled)
        {
            return (false, "VPN-сервис временно недоступен.", null, false);
        }

        var normalized = code?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (false, "Укажите промо-код.", null, false);
        }

        var promo = await _vpnRepository.GetByCodeAsync(normalized, cancellationToken);
        if (promo == null || promo.IsUsed)
        {
            return (false, "Неверный или уже использованный промо-код.", null, false);
        }

        // Длительность ключа в месяцах (по умолчанию 1).
        var months = promo.DurationMonths > 0 ? promo.DurationMonths : 1;

        // Расширяем текущую активную подписку; если её нет — создаём новую.
        var active = await _vpnRepository.GetActiveAsync(userId, cancellationToken);
        var clientUuid = promo.ClientUuid ?? Guid.NewGuid().ToString();
        DateTime expiresAtUtc;

        if (active == null || active.IsRevoked || (active.ExpiresAtUtc != null && active.ExpiresAtUtc <= DateTime.UtcNow))
        {
            expiresAtUtc = DateTime.UtcNow.AddMonths(months);
        }
        else
        {
            // Продлеваем действующую подписку с базовой точки.
            var baseTime = active.ExpiresAtUtc ?? DateTime.UtcNow;
            if (baseTime < DateTime.UtcNow)
            {
                baseTime = DateTime.UtcNow;
            }
            clientUuid = active.ClientUuid ?? clientUuid;
            expiresAtUtc = baseTime.AddMonths(months);
        }

                // ===== АТОМАРНОСТЬ =====
        // Сначала провижионируем/продлеваем клиента на панели 3X-UI. Только при
        // подтверждении от 3X-UI сохраняем изменения в PostgreSQL (активируем промо).
        // Если панель не сконфигурирована или недоступна — промо НЕ помечается
        // использованным, подписка НЕ создаётся/не продлевается, на фронт уходит
        // HTTP 502, а «мёртвая» ссылка не выдаётся.
        if (!IsPanelReady())
        {
            _logger.LogError(
                "3X-UI: панель не сконфигурирована (VPN_ENABLED=true, но отсутствует " +
                "VPN_PANEL_API_URL или VPN_PANEL_API_TOKEN). Промо-код для пользователя {UserId} отклонён.",
                userId);
            _vpnConfig.LogDiagnostics();
            return (false, "Не удалось активировать промо-код: VPN-провайдер не настроен.", null, true);
        }

        bool provisioned;
        try
        {
            provisioned = await _panelClient.UpsertClientAsync(
                clientUuid,
                BuildPanelEmail(userId),
                expiresAtUtc,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "3X-UI: сбой при активации промо-кода для пользователя {UserId}.", userId);
            return (false, "Не удалось активировать промо-код: VPN-провайдер недоступен.", null, true);
        }

        if (!provisioned)
        {
            _logger.LogWarning(
                "3X-UI: не удалось активировать клиента по промо-коду (success=false) для пользователя {UserId}.",
                userId);
            return (false, "Не удалось активировать промо-код: ошибка VPN-провайдера.", null, true);
        }

        // Панель успешно провижионировала клиента — теперь атомарно сохраняем в БД
        // изменения подписки (создание или продление) и пометку промо-кода использованным.
        var newSubscriptionId = promo.SubscriptionId;
        VpnSubscriptionEntity? newSubscription = null;
        VpnSubscriptionEntity? existingSubscription = null;

        if (active == null || active.IsRevoked || (active.ExpiresAtUtc != null && active.ExpiresAtUtc <= DateTime.UtcNow))
        {
            newSubscription = new VpnSubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = "promo",
                PlanName = $"BronyVPN {months} мес.",
                StartedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = expiresAtUtc,
                ClientUuid = clientUuid,
                PanelPlanNameId = $"{months}-month"
            };
            newSubscriptionId = newSubscription.Id;
        }
        else
        {
            // Продлеваем действующую подписку.
            active.ExpiresAtUtc = expiresAtUtc;
            active.ClientUuid = clientUuid;
            existingSubscription = active;
        }

        promo.IsUsed = true;
        promo.UsedAtUtc = DateTime.UtcNow;
        promo.UsedByUserId = userId;
        promo.SubscriptionId = newSubscriptionId;

        await _vpnRepository.CompletePromoActivationAsync(
            newSubscription,
            existingSubscription,
            promo,
            cancellationToken);

        var result = await GetStatusAsync(userId, cancellationToken);
        return (true, null, new VpnPromoActivateResponse
        {
            Success = true,
            PlanName = result.PlanName,
            ExpiresAtUtc = result.ExpiresAtUtc
        }, false);
    }

    public async Task<(bool Success, string? Error, bool ServerError)> GrantDaysAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days <= 0)
        {
            return (false, "Некорректное количество дней награды.", false);
        }

        if (!_optionsAccessor.Value.Enabled)
        {
            return (false, "VPN-сервис временно недоступен.", false);
        }

        if (!IsPanelReady())
        {
            _logger.LogError(
                "3X-UI: панель не сконфигурирована, награда VPN для пользователя {UserId} отклонена.",
                userId);
            return (false, "Не удалось начислить VPN-дни: VPN-провайдер не настроен.", true);
        }

        var now = DateTime.UtcNow;
        var email = BuildPanelEmail(userId);

        // Локальная подписка (tracked) — для получения известного UUID и последующей синхронизации.
        var local = await _vpnRepository.GetActiveTrackedAsync(userId, cancellationToken);
        var localUuid = local?.ClientUuid;

        // 1. Текущий клиент из панели 3X-UI (по UUID и/или email).
        XuiClientInfo? panelInfo = null;
        try
        {
            panelInfo = await _panelClient.GetClientInfoAsync(
                localUuid ?? string.Empty,
                email,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "3X-UI: не удалось прочитать текущего клиента {Email}; считаем, что клиента нет.",
                email);
        }

        var oldExpiry = panelInfo?.ExpiryUtc;
        var clientUuid = panelInfo?.Uuid ?? localUuid ?? Guid.NewGuid().ToString();

        // 2. Новый expiry: активный клиент с будущим expiry → прибавляем дни к нему,
        //    иначе (не найден/просрочен/без expiry) — от текущего момента.
        DateTime newExpiry;
        if (oldExpiry.HasValue && oldExpiry.Value > now)
        {
            newExpiry = oldExpiry.Value.AddDays(days);
        }
        else
        {
            newExpiry = now.AddDays(days);
        }

        _logger.LogInformation(
            "3X-UI: начисление {Days} дней VPN пользователю {UserId} ({Email}). oldExpiry={OldExpiry} -> newExpiry={NewExpiry}",
            days,
            userId,
            email,
            oldExpiry.HasValue ? oldExpiry.Value.ToString("O") : "<нет/просрочен>",
            newExpiry.ToString("O"));

        // 3. Обновляем/создаём клиента на панели через тот же вызов, что и промо-активация.
        bool provisioned;
        try
        {
            provisioned = await _panelClient.UpsertClientAsync(
                clientUuid,
                email,
                newExpiry,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "3X-UI: сбой при начислении VPN-дней пользователю {UserId}.", userId);
            return (false, "Не удалось начислить VPN-дни: VPN-провайдер недоступен.", true);
        }

        if (!provisioned)
        {
            _logger.LogWarning("3X-UI: не удалось начислить VPN-дни (success=false) пользователю {UserId}.", userId);
            return (false, "Не удалось начислить VPN-дни: ошибка VPN-провайдера.", true);
        }

        // 4. Синхронизируем локальную подписку с новым состоянием панели.
        if (local != null && !local.IsRevoked && (local.ExpiresAtUtc == null || local.ExpiresAtUtc > now))
        {
            local.ExpiresAtUtc = newExpiry;
            local.ClientUuid = clientUuid;
            await _vpnRepository.UpdateSubscriptionAsync(local, cancellationToken);
        }
        else
        {
            await _vpnRepository.CreateSubscriptionAsync(new VpnSubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = "reward",
                PlanName = "BronyVPN (награда за стрик)",
                StartedAtUtc = now,
                ExpiresAtUtc = newExpiry,
                ClientUuid = clientUuid,
                PanelPlanNameId = "reward"
            }, cancellationToken);
        }

        return (true, null, false);
    }

    public async Task RevokeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await _vpnRepository.GetActiveAsync(userId, cancellationToken);
        await _vpnRepository.RevokeAsync(userId, cancellationToken);

        if (active != null && !string.IsNullOrWhiteSpace(active.ClientUuid))
        {
            await _panelClient.RemoveClientAsync(active.ClientUuid, cancellationToken);
        }
    }

    /// <summary>
    /// Детерминированный email клиента на панели 3X-UI: "BronyVPN-" + первые 8 символов
    /// UUID пользователя. Один и тот же userId всегда даёт один и тот же email.
    /// </summary>
    private static string BuildPanelEmail(Guid userId)
        => $"BronyVPN-{userId.ToString("N")[..8]}";

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
