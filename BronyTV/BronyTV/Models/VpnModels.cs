using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BronyTV.Models;

/// <summary>
/// VPN-конфигурация (настройки пакетов, промо-коды, дефолты).
/// Алфавит и длины константами — без внешних зависимостей, чтобы
/// ссылки можно было строить локально.
/// </summary>
public static class VpnConfig
{
    // Безопасный алфавит без похожих символов (0/O, 1/I/l), чтобы ключ
    // можно было без ошибок перепечатать вручную.
    private const string PromoAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public const int PromoCodeLength = 12;

    // Стандартное время жизни trial-подписки (14 дней).
    public static readonly TimeSpan TrialDuration = TimeSpan.FromDays(14);
    public const int TrialNameId = 1; // 1 месяц на 3X-UI.

    /// <summary>
    /// Генерирует криптографически стойкий промо-код.
    /// </summary>
    public static string GeneratePromoCode()
    {
        var chars = new char[PromoCodeLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = PromoAlphabet[RandomNumberGenerator.GetInt32(PromoAlphabet.Length)];
        }
        return new string(chars);
    }

    /// <summary>
    /// Допустимые длительности промо-ключа в месяцах.
    /// </summary>
    public static readonly int[] AllowedPromoDurations = { 1, 3, 6, 12 };

    /// <summary>
    /// Проверяет, что переданная длительность промо-ключа — одна из допустимых (1, 3, 6, 12).
    /// </summary>
    public static bool IsValidPromoDuration(int durationMonths) =>
        Array.IndexOf(AllowedPromoDurations, durationMonths) >= 0;
}

/// <summary>
/// Помощник для сборки ссылки VLESS (xray / v2ray). UUID покупателя приходит
/// из внешнего источника; строим валидный URI-хост с параметрами gRPC Reality.
/// </summary>
public static class VlessLinkBuilder
{
    /// <summary>
    /// Собирает строку подключения для gRPC Reality:
    /// <c>vless://{UUID}@{HOST}:{PORT}?type=grpc&amp;serviceName=grpc-service&amp;security=reality&amp;pbk={PUBLIC_KEY}&amp;fp=chrome&amp;sni={SNI}&amp;sid={SHORT_ID}#BronyVPN</c>
    /// База параметров берётся из конфигурации, а обязательные для Reality gRPC
    /// поля (type, serviceName, security, fp) гарантированно добавляются принудительно.
    /// flow не используется, так как транспорт — gRPC.
    /// </summary>
    public static string Build(
        string uuid,
        string host,
        int port,
        string? parameters,
        string remark = "BronyVPN")
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            throw new ArgumentException("UUID не может быть пустым.", nameof(uuid));
        }

        var hostCleaned = (host ?? "127.0.0.1").Trim();
        if (hostCleaned.Length == 0)
        {
            hostCleaned = "127.0.0.1";
        }
        if (port <= 0 || port > 65535)
        {
            port = 443;
        }

        var builder = new StringBuilder();
        builder.Append("vless://");
        builder.Append(Uri.EscapeDataString(uuid.Trim()));
        builder.Append('@');
        builder.Append(Uri.EscapeDataString(hostCleaned));
        builder.Append(':');
        builder.Append(port);

        // Гарантируем корректный набор параметров gRPC Reality даже при неполной конфигурации.
        var normalized = NormalizeRealityParams(parameters ?? string.Empty);
        if (normalized.Length > 0)
        {
            builder.Append('?');
            builder.Append(normalized);
        }

        var safeRemark = string.IsNullOrWhiteSpace(remark) ? "BronyVPN" : remark.Trim();
        builder.Append('#');
        builder.Append(Uri.EscapeDataString(safeRemark));

        return builder.ToString();
    }

    /// <summary>
    /// Приводит параметры к каноническому виду gRPC Reality: гарантирует наличие
    /// <c>type=grpc</c>, <c>serviceName=grpc-service</c>, <c>security=reality</c>
    /// и дефолтного <c>fp=chrome</c> (если fingerprint не задан). Параметр
    /// <c>flow</c> НЕ добавляется — для gRPC он не используется. Не дублирует уже
    /// заданные параметры, а каждому ключу сохраняет первое (приоритетное) значение.
    /// </summary>
    private static string NormalizeRealityParams(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = string.Empty;
        }

        var parts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = token.IndexOf('=');
            var key = idx >= 0 ? token[..idx] : token;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            // flow явно исключаем: транспорт gRPC не поддерживает xtls-rprx-vision.
            if (string.Equals(key, "flow", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add(key))
            {
                parts.Add(token);
            }
        }

        void Ensure(string key, string value)
        {
            if (seen.Add(key))
            {
                parts.Add($"{key}={value}");
            }
        }

        // Для Reality всегда gRPC-транспорт с дефолтным serviceName и chrome-отпечатком.
        Ensure("type", "grpc");
        Ensure("serviceName", "grpc-service");
        Ensure("security", "reality");
        Ensure("fp", "chrome");

        return string.Join("&", parts);
    }
}
