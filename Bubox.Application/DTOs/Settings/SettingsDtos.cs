namespace Bubox.Application.DTOs.Settings;

public record OrderSettingsDto(
    int CutoffDaysBefore,
    string CutoffTime,
    string TimeZone,
    int PaymentTimeoutMinutes,
    decimal DeliveryFee,
    DateTime? UpdatedAt
);

public record UpdateOrderSettingsRequest(
    int CutoffDaysBefore,
    string CutoffTime,
    string TimeZone,
    int PaymentTimeoutMinutes,
    decimal DeliveryFee
);

public record OrderCutoffInfoDto(
    int CutoffDaysBefore,
    string CutoffTime,
    string TimeZone,
    DateTime ServerTimeUtc,
    DateOnly EarliestDeliveryDate,
    DateOnly LatestDeliveryDate,
    decimal DeliveryFee
);
