namespace Bubox.Application.DTOs.Settings;

/// <summary>Satu tingkat tarif per hari: berlaku mulai <see cref="MinDays"/> hari kirim dalam satu pesanan.</summary>
public record FeeTierDto(int MinDays, decimal PerDay);

/// <summary>Aturan ongkir: Mode FREE / FIXED (Amount per pesanan) / PER_DAY (Tiers).</summary>
public record FulfillmentFeeRuleDto(string Mode, decimal Amount, List<FeeTierDto>? Tiers);

public record OrderSettingsDto(
    int CutoffDaysBefore,
    string CutoffTime,
    string TimeZone,
    int PaymentTimeoutMinutes,
    FulfillmentFeeRuleDto DeliveryFeeRule,
    FulfillmentFeeRuleDto PickupFeeRule,
    string ServiceFeeType,
    decimal ServiceFeeValue,
    decimal TaxPercent,
    DateTime? UpdatedAt
);

public record UpdateOrderSettingsRequest(
    int CutoffDaysBefore,
    string CutoffTime,
    string TimeZone,
    int PaymentTimeoutMinutes,
    /// <summary>Aturan ongkir Pesan Antar.</summary>
    FulfillmentFeeRuleDto DeliveryFeeRule,
    /// <summary>Aturan biaya Ambil Sendiri.</summary>
    FulfillmentFeeRuleDto PickupFeeRule,
    /// <summary>PERCENT atau FIXED.</summary>
    string ServiceFeeType,
    /// <summary>Persen (maks 2 desimal, 0-100) atau Rupiah bulat, sesuai ServiceFeeType.</summary>
    decimal ServiceFeeValue,
    /// <summary>Persen (maks 2 desimal, 0-100).</summary>
    decimal TaxPercent
);

public record OrderCutoffInfoDto(
    int CutoffDaysBefore,
    string CutoffTime,
    string TimeZone,
    DateTime ServerTimeUtc,
    DateOnly EarliestDeliveryDate,
    DateOnly LatestDeliveryDate,
    // Untuk perkiraan total di checkout (server tetap menghitung ulang)
    FulfillmentFeeRuleDto DeliveryFeeRule,
    FulfillmentFeeRuleDto PickupFeeRule,
    string ServiceFeeType,
    decimal ServiceFeeValue,
    decimal TaxPercent
);
