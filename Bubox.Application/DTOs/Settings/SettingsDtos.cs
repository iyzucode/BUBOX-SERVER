namespace Bubox.Application.DTOs.Settings;

public record OrderSettingsDto(
    int CutoffDaysBefore,
    string CutoffTime,
    string TimeZone,
    int PaymentTimeoutMinutes,
    decimal DeliveryFee,
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
    decimal DeliveryFee,
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
    decimal DeliveryFee,
    // Untuk perkiraan total di checkout (server tetap menghitung ulang)
    string ServiceFeeType,
    decimal ServiceFeeValue,
    decimal TaxPercent
);
