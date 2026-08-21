using System;
using System.Security.Cryptography;

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
    private const string DefaultPublicKey = "1T9mTOWKnoaac7x0u5e8Ipt3QiyznLjxoOg1F2LjECo";
    private const string DefaultSni = "www.samsung.com";
    private const string DefaultShortId = "8556ec";

    /// <summary>
    /// Собирает строку подключения для gRPC Reality строго детерминированно:
    /// <c>vless://{UUID}@{HOST}:{PORT}?type=grpc&amp;serviceName=grpc-service&amp;security=reality&amp;pbk={PUBLIC_KEY}&amp;fp=chrome&amp;sni={SNI}&amp;sid={SHORT_ID}#BronyVPN</c>
    /// Значения pbk/sni/sid берутся из переданных параметров (VlessParameters) либо
    /// из безопасных дефолтов с корректными длинами (sid — короткий hex, pbk — публичный
    /// ключ Reality). Разделитель query-параметров — строго одиночный символ '&'.
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

        var pbk = ExtractParam(parameters, "pbk", DefaultPublicKey);
        var sni = ExtractParam(parameters, "sni", DefaultSni);
        var sid = ExtractParam(parameters, "sid", DefaultShortId);

        var safeHost = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        var safePort = port > 0 && port <= 65535 ? port : 443;
        var safeRemark = string.IsNullOrWhiteSpace(remark) ? "BronyVPN" : remark.Trim();

        var query = "type=grpc&serviceName=grpc-service&security=reality"
                    + "&pbk=" + pbk
                    + "&fp=chrome"
                    + "&sni=" + sni
                    + "&sid=" + sid;

        return "vless://" + Uri.EscapeDataString(uuid.Trim())
               + "@" + Uri.EscapeDataString(safeHost)
               + ":" + safePort
               + "?" + query
               + "#" + Uri.EscapeDataString(safeRemark);
    }

    /// <summary>
    /// Достаёт значение параметра <paramref name="key"/> из строки query-параметров
    /// (простое разбиение по '&' и '=', без регулярок). Если параметр отсутствует или
    /// имеет пустое значение — возвращается дефолт.
    /// </summary>
    private static string ExtractParam(string? raw, string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        foreach (var token in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = token.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var k = token[..idx];
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var v = token[(idx + 1)..].Trim();
            return string.IsNullOrWhiteSpace(v) ? fallback : v;
        }

        return fallback;
    }
}
