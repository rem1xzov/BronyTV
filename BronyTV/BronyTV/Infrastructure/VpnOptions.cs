namespace BronyTV.Infrastructure;

/// <summary>
/// VPN-настройки, читаемые из конфигурации (appsettings.json / переменные окружения).
/// Секретные значения (например, API-ключ панели 3X-UI) задаются только через
/// окружение/docker-compose, никогда не зашиваются в код.
/// </summary>
public class VpnOptions
{
    public const string SectionName = "Vpn";

    /// <summary>Включён ли раздел VPN на платформе. Если выключен, разделы прячутся.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Публичный хост сервера VLESS (для ссылки подключения).</summary>
    public string? ServerHost { get; set; }

    /// <summary>Порт VLESS.</summary>
    public int ServerPort { get; set; } = 443;

    /// <summary>Параметры после ? в VLESS-ссылке (например security=reality&amp;sni=...).</summary>
    public string? VlessParameters { get; set; }

    /// <summary>Базовый URL панели 3X-UI (для перехода на клиентский кабинет).</summary>
    public string? PanelBaseUrl { get; set; }

    /// <summary>Домен панели 3X-UI (для ссылки на скачивание клиентов).</summary>
    public string? ClientDomain { get; set; }

    /// <summary>Длительность trial-подписки в днях (по умолчанию 7).</summary>
    public int TrialDays { get; set; } = 7;

    /// <summary>Максимум промо-кодов, отображаемых в админке за раз.</summary>
    public int AdminPromoPageSize { get; set; } = 100;

    /// <summary>Путь для перехода на панель 3X-UI.</summary>
    public string? PanelPath { get; set; } = "/";
}
