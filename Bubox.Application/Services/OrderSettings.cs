using Bubox.Domain.Constants;

namespace Bubox.Application.Services;

/// <summary>
/// Pengaturan order yang sedang berlaku (hasil baca app_settings + default),
/// beserta perhitungan cutoff berbasis zona waktu.
/// </summary>
public sealed class OrderSettings
{
    public const int DefaultCutoffDaysBefore = 1;
    public static readonly TimeOnly DefaultCutoffTime = new(17, 0);
    public const string DefaultTimeZone = "Asia/Jakarta";
    public const int DefaultPaymentTimeoutMinutes = 60;
    public const decimal DefaultDeliveryFee = 0m;

    public int CutoffDaysBefore { get; init; } = DefaultCutoffDaysBefore;
    public TimeOnly CutoffTime { get; init; } = DefaultCutoffTime;
    public string TimeZoneId { get; init; } = DefaultTimeZone;
    public int PaymentTimeoutMinutes { get; init; } = DefaultPaymentTimeoutMinutes;
    public decimal DeliveryFee { get; init; } = DefaultDeliveryFee;
    /// <summary>ServiceFeeTypes.Percent / Fixed.</summary>
    public string ServiceFeeType { get; init; } = ServiceFeeTypes.Fixed;
    /// <summary>Persen (PERCENT) atau Rupiah (FIXED). 0 = tanpa biaya layanan.</summary>
    public decimal ServiceFeeValue { get; init; }
    /// <summary>Pajak dalam persen. 0 = tanpa pajak.</summary>
    public decimal TaxPercent { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public TimeZoneInfo TimeZone => ResolveTimeZone(TimeZoneId);

    /// <summary>Waktu terakhir (UTC) sebuah tanggal kirim masih boleh dipesan.</summary>
    public DateTime GetCutoffUtc(DateOnly deliveryDate)
    {
        var localCutoff = deliveryDate.AddDays(-CutoffDaysBefore).ToDateTime(CutoffTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localCutoff, TimeZone);
    }

    public bool IsOrderable(DateOnly deliveryDate, DateTime nowUtc) => nowUtc < GetCutoffUtc(deliveryDate);

    public DateOnly GetLocalToday(DateTime nowUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZone));

    public DateOnly GetEarliestDeliveryDate(DateTime nowUtc)
    {
        var candidate = GetLocalToday(nowUtc);
        while (!IsOrderable(candidate, nowUtc))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (TryFindTimeZone(timeZoneId, out var zone))
        {
            return zone;
        }

        // Fallback aman bila tzdata tidak tersedia di server: WIB (UTC+7, tanpa DST)
        return TimeZoneInfo.CreateCustomTimeZone("WIB", TimeSpan.FromHours(7), "WIB", "WIB");
    }

    public static bool TryFindTimeZone(string timeZoneId, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    /// <summary>1: Senin ... 7: Minggu (sama dengan menu_assignments.day_of_week).</summary>
    public static int ToDayOfWeekNumber(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    public static string GetDayName(DateOnly date) => MenuConstants.GetDayName(ToDayOfWeekNumber(date));
}
