using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<VpnConfigResolver> _logger;

    public VpnConfigResolver(IConfiguration configuration, ILogger<VpnConfigResolver> logger)
    {
        _configuration = configuration;
        _logger = logger;
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

    /// <summary>
    /// Диагностика: логирует каждый источник конфигурации по отдельности (длина строки
    /// и первые/последние 3 символа — без полного значения), чтобы понять, какой именно
    /// провайдер отдаёт null/empty, а какой возвращает значение.
    /// </summary>
    public void LogDiagnostics()
    {
        _logger.LogInformation("VPN-DIAG: PanelApiUrl источники:");
        LogSource("Vpn:PanelApiUrl (IConfiguration)", _configuration["Vpn:PanelApiUrl"]);
        LogSource("VPN_PANEL_API_URL (IConfiguration)", _configuration["VPN_PANEL_API_URL"]);
        LogSource("VPN_PANEL_API_URL (Environment)", Environment.GetEnvironmentVariable("VPN_PANEL_API_URL"));

        _logger.LogInformation("VPN-DIAG: PanelApiToken источники:");
        LogSource("Vpn:PanelApiToken (IConfiguration)", _configuration["Vpn:PanelApiToken"], reveal: false);
        LogSource("VPN_PANEL_API_TOKEN (IConfiguration)", _configuration["VPN_PANEL_API_TOKEN"], reveal: false);
        LogSource("VPN_PANEL_API_TOKEN (Environment)", Environment.GetEnvironmentVariable("VPN_PANEL_API_TOKEN"), reveal: false);
    }

    private void LogSource(string label, string? value, bool reveal = true)
    {
        _logger.LogInformation("VPN-DIAG: {Label} => {Info}", label, Describe(value, reveal));
    }

    private static string Describe(string? value, bool reveal)
    {
        if (value is null)
        {
            return "<null>";
        }

        if (value.Length == 0)
        {
            return "<empty>";
        }

        if (!reveal)
        {
            return $"len={value.Length}";
        }

        var head = value.Length <= 3 ? value : value[..3];
        var tail = value.Length <= 3 ? string.Empty : value[^3..];
        return $"len={value.Length}, head='{head}', tail='{tail}'";
    }
}
