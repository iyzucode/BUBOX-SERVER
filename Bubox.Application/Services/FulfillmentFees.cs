using System.Globalization;
using System.Text.Json;
using Bubox.Application.DTOs.Settings;
using Bubox.Application.Exceptions;
using Bubox.Domain.Constants;

namespace Bubox.Application.Services;

/// <summary>Hasil perhitungan ongkir sebuah pesanan.</summary>
public readonly record struct FulfillmentFeeResult(decimal Fee, string? Note);

/// <summary>
/// Aturan ongkir per metode pemesanan (Pesan Antar / Ambil Sendiri):
///   FREE    → Rp 0
///   FIXED   → nominal per pesanan
///   PER_DAY → jumlah hari × tarif tingkat yang berlaku. Tingkat = minDays terbesar yang ≤ jumlah hari,
///             dan SELURUH hari memakai tarif tingkat itu (mis. 10 hari pada tingkat "mulai 7 hari Rp 1.500"
///             = 10 × 1.500).
/// Rumus yang sama dipakai client (features/order/fulfillmentFee.ts) untuk perkiraan di checkout.
/// </summary>
public static class FulfillmentFees
{
    public const int MaxTiers = 10;
    public const decimal MaxAmount = 1_000_000m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public static FulfillmentFeeRuleDto Free => new(FulfillmentFeeModes.Free, 0m, []);

    public static FulfillmentFeeResult Compute(FulfillmentFeeRuleDto rule, int days)
    {
        switch (rule.Mode)
        {
            case FulfillmentFeeModes.Fixed:
                return new FulfillmentFeeResult(rule.Amount, null);
            case FulfillmentFeeModes.PerDay when days > 0:
                var tier = (rule.Tiers ?? [])
                    .Where(t => t.MinDays <= days)
                    .OrderByDescending(t => t.MinDays)
                    .FirstOrDefault();
                if (tier == null || tier.PerDay <= 0)
                {
                    return new FulfillmentFeeResult(0m, null);
                }
                return new FulfillmentFeeResult(days * tier.PerDay, $"{days} hari × Rp {tier.PerDay.ToString("N0", Indonesian)}");
            default:
                return new FulfillmentFeeResult(0m, null);
        }
    }

    /// <summary>Validasi & rapikan aturan dari admin (urutkan tingkat, buang data yang tidak dipakai mode).</summary>
    public static FulfillmentFeeRuleDto Normalize(FulfillmentFeeRuleDto? rule, string label)
    {
        if (rule == null)
        {
            throw new InvalidSettingException($"Aturan biaya {label} wajib diisi.");
        }

        var mode = rule.Mode?.Trim().ToUpperInvariant() ?? string.Empty;
        switch (mode)
        {
            case FulfillmentFeeModes.Free:
                return Free;

            case FulfillmentFeeModes.Fixed:
                if (!IsRupiah(rule.Amount))
                {
                    throw new InvalidSettingException($"Biaya {label} (nominal) harus bilangan bulat Rupiah antara 0 sampai 1.000.000.");
                }
                return new FulfillmentFeeRuleDto(mode, rule.Amount, []);

            case FulfillmentFeeModes.PerDay:
                var tiers = (rule.Tiers ?? []).OrderBy(t => t.MinDays).ToList();
                if (tiers.Count is 0 or > MaxTiers)
                {
                    throw new InvalidSettingException($"Tarif per hari {label} harus punya 1 sampai {MaxTiers} tingkat.");
                }
                if (tiers[0].MinDays != 1)
                {
                    throw new InvalidSettingException($"Tingkat pertama tarif {label} harus mulai dari 1 hari.");
                }
                if (tiers.Any(t => t.MinDays is < 1 or > OrderLimits.MaxDaysPerOrder))
                {
                    throw new InvalidSettingException($"Batas hari tarif {label} harus antara 1 sampai {OrderLimits.MaxDaysPerOrder}.");
                }
                if (tiers.Select(t => t.MinDays).Distinct().Count() != tiers.Count)
                {
                    throw new InvalidSettingException($"Batas hari tarif {label} tidak boleh sama.");
                }
                if (tiers.Any(t => !IsRupiah(t.PerDay)))
                {
                    throw new InvalidSettingException($"Tarif per hari {label} harus bilangan bulat Rupiah antara 0 sampai 1.000.000.");
                }
                return new FulfillmentFeeRuleDto(mode, 0m, tiers.Select(t => new FeeTierDto(t.MinDays, t.PerDay)).ToList());

            default:
                throw new InvalidSettingException($"Cara hitung biaya {label} harus FREE, FIXED, atau PER_DAY.");
        }
    }

    /// <summary>Baca dari app_settings; nilai rusak / kosong dianggap gratis.</summary>
    public static FulfillmentFeeRuleDto Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Free;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<FulfillmentFeeRuleDto>(json, JsonOptions);
            return Normalize(parsed, "ongkir");
        }
        catch (Exception ex) when (ex is JsonException or InvalidSettingException)
        {
            return Free;
        }
    }

    public static string Serialize(FulfillmentFeeRuleDto rule) => JsonSerializer.Serialize(rule, JsonOptions);

    private static bool IsRupiah(decimal value) => value >= 0 && value <= MaxAmount && value == decimal.Truncate(value);
}
