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

    // Стандартное время жизни trial-подписки (7 дней).
    public static readonly TimeSpan TrialDuration = TimeSpan.FromDays(7);
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
}

/// <summary>
/// Помощник для сборки ссылки VLESS (xray / v2ray). UUID покупателя приходит
/// из внешнего источника; строим только валидный URI-хост с параметрами.
/// </summary>
public static class VlessLinkBuilder
{
    /// <summary>
    /// Собирает строку подключения вида vless://uuid@host:port?security=reality...
    /// </summary>
    public static string Build(
        string uuid,
        string host,
        int port,
        string? parameters,
        string remark = "")
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

        if (!string.IsNullOrWhiteSpace(parameters))
        {
            var trimmed = parameters.Trim().TrimStart('?');
            if (trimmed.Length > 0)
            {
                builder.Append('?');
                builder.Append(trimmed);
            }
        }

        if (string.IsNullOrWhiteSpace(remark))
        {
            builder.Append("#BronyVPN");
        }
        else
        {
            builder.Append('#');
            builder.Append(Uri.EscapeDataString(remark));
        }

        return builder.ToString();
    }
}
