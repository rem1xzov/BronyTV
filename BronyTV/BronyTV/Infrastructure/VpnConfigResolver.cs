using Microsoft.Extensions.Configuration;

namespace BronyTV.Infrastructure;

/// <summary>
/// Единая точка чтения настроек интеграции с 3X-UI из <see cref="IConfiguration"/>.
/// Поддерживает секционные ключи (<c>Vpn:PanelApiUrl</c> / <c>Vpn__PanelApiUrl</c>)
/// и плоские переменные окружения (<c>VPN_PANEL_API_URL</c> и т.п.), чтобы одна и та же
/// конфигурация работала в docker-compose, systemd и локальной разработке.
/// </summary>
public sealed class VpnConfigResolver
{
    private readonly IConfiguration _configuration;

    public VpnConfigResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Полный URL панели 3X-UI, включая секретный web-префикс
    /// (например <c>https://ip:port/TugsFcqj7OslFxFadz</c>).
    /// </summary>
    public string? PanelApiUrl =>
        _configuration["Vpn:PanelApiUrl"]
        ?? _configuration["VPN_PANEL_API_URL"]
        ?? Environment.GetEnvironmentVariable("VPN_PANEL_API_URL");

    /// <summary>Bearer-токен API панели 3X-UI.</summary>
    public string? PanelApiToken =>
        _configuration["Vpn:PanelApiToken"]
        ?? _configuration["VPN_PANEL_API_TOKEN"]
        ?? Environment.GetEnvironmentVariable("VPN_PANEL_API_TOKEN");

    /// <summary>ID инбаунда (VLESS) на 3X-UI, куда добавляются клиенты (по умолчанию 2).</summary>
    public int InboundId =>
        _configuration.GetValue<int?>("Vpn:PanelInboundId")
        ?? _configuration.GetValue<int?>("VPN_PANEL_INBOUND_ID")
        ?? 2;
}
