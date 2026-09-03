using System;
using System.Security.Cryptography;

namespace BronyTV.Service;

/// <summary>Тип приза колеса фортуны.</summary>
public enum FortunePrizeType
{
    Vpn30Days,
    Premium1Year,
    Vpn1Year,
    Nft
}

/// <summary>Один сектор колеса фортуны.</summary>
public sealed record FortunePrize(
    FortunePrizeType Type,
    string Description,
    int Weight,
    int VpnDays,
    int PremiumDays);

/// <summary>
/// Сектора колеса фортуны. Веса — согласованные проценты (в сумме ровно 100%):
/// 30 дней VPN — 69%, 1 год премиум — 25%, 1 год VPN — 5%, NFT — 1%.
/// Рандом генерируется на сервере (RandomNumberGenerator).
/// </summary>
public static class FortuneWheelPrizes
{
    public static readonly FortunePrize[] All =
    {
        new(FortunePrizeType.Vpn30Days, "30 дней VPN", 69, 30, 0),
        new(FortunePrizeType.Premium1Year, "1 год премиум", 25, 0, 365),
        new(FortunePrizeType.Vpn1Year, "1 год VPN", 5, 365, 0),
        new(FortunePrizeType.Nft, "NFT-подарок в Telegram", 1, 0, 0)
    };

    private static readonly int TotalWeight = SumWeights();

    private static int SumWeights()
    {
        var total = 0;
        foreach (var prize in All)
        {
            total += prize.Weight;
        }

        return total;
    }

    /// <summary>
    /// Криптографически стойкий выбор приза по весам. Возвращает (приз, индекс).
    /// </summary>
    public static (FortunePrize Prize, int Index) PickRandom()
    {
        var roll = RandomNumberGenerator.GetInt32(TotalWeight);

        var cumulative = 0;
        for (var i = 0; i < All.Length; i++)
        {
            cumulative += All[i].Weight;
            if (roll < cumulative)
            {
                return (All[i], i);
            }
        }

        return (All[^1], All.Length - 1);
    }

    public static string PrizeTypeKey(FortunePrizeType type) => type switch
    {
        FortunePrizeType.Vpn30Days => "vpn30",
        FortunePrizeType.Premium1Year => "premium1y",
        FortunePrizeType.Vpn1Year => "vpn1y",
        FortunePrizeType.Nft => "nft",
        _ => "unknown"
    };
}
