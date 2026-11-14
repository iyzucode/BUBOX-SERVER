using System.Globalization;
using Bubox.Application.DTOs.Settings;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class SettingsService(IAppSettingRepository settingRepository) : ISettingsService
{
    private const string TimeFormat = "HH:mm";

    public async Task<OrderSettings> GetEffectiveOrderSettingsAsync()
    {
        var settings = (await settingRepository.GetAllAsync()).ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

        string? Get(string key) => settings.TryGetValue(key, out var s) ? s.Value : null;

        return new OrderSettings
        {
            CutoffDaysBefore = int.TryParse(Get(AppSettingKeys.OrderCutoffDaysBefore), out var days) && days >= 0
                ? days
                : OrderSettings.DefaultCutoffDaysBefore,
            CutoffTime = TimeOnly.TryParseExact(Get(AppSettingKeys.OrderCutoffTime), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? time
                : OrderSettings.DefaultCutoffTime,
            TimeZoneId = string.IsNullOrWhiteSpace(Get(AppSettingKeys.OrderTimezone))
                ? OrderSettings.DefaultTimeZone
                : Get(AppSettingKeys.OrderTimezone)!,
            PaymentTimeoutMinutes = int.TryParse(Get(AppSettingKeys.PaymentTimeoutMinutes), out var timeout) && timeout > 0
                ? timeout
                : OrderSettings.DefaultPaymentTimeoutMinutes,
            DeliveryFeeRule = FulfillmentFees.Parse(Get(AppSettingKeys.DeliveryFeeRule)),
            PickupFeeRule = FulfillmentFees.Parse(Get(AppSettingKeys.PickupFeeRule)),
            ServiceFeeType = ServiceFeeTypes.All.Contains(Get(AppSettingKeys.ServiceFeeType) ?? string.Empty)
                ? Get(AppSettingKeys.ServiceFeeType)!
                : ServiceFeeTypes.Fixed,
            ServiceFeeValue = ParseNonNegative(Get(AppSettingKeys.ServiceFeeValue)),
            TaxPercent = Math.Min(100m, ParseNonNegative(Get(AppSettingKeys.TaxPercent))),
            UpdatedAt = settings.Count == 0 ? null : settings.Values.Max(s => s.UpdatedAt)
        };
    }

    private static decimal ParseNonNegative(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0 ? d : 0m;

    /// <summary>Persen 0-100 dengan maksimal 2 angka desimal.</summary>
    private static bool IsValidPercent(decimal value) => value is >= 0 and <= 100 && decimal.Round(value, 2) == value;

    public async Task<OrderSettingsDto> GetOrderSettingsAsync() => ToDto(await GetEffectiveOrderSettingsAsync());

    public async Task<OrderSettingsDto> UpdateOrderSettingsAsync(UpdateOrderSettingsRequest request, Guid updatedBy)
    {
        if (request.CutoffDaysBefore is < 0 or > 7)
        {
            throw new InvalidSettingException("Hari cutoff harus antara 0 (hari H) sampai 7 (H-7).");
        }

        if (!TimeOnly.TryParseExact(request.CutoffTime?.Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cutoffTime))
        {
            throw new InvalidSettingException("Jam cutoff harus berformat HH:mm, contoh 17:00.");
        }

        var timeZoneId = request.TimeZone?.Trim() ?? string.Empty;
        if (!OrderSettings.TryFindTimeZone(timeZoneId, out _))
        {
            throw new InvalidSettingException($"Zona waktu '{timeZoneId}' tidak dikenali. Gunakan format IANA, contoh Asia/Jakarta.");
        }

        if (request.PaymentTimeoutMinutes is < 5 or > 1440)
        {
            throw new InvalidSettingException("Batas waktu bayar harus antara 5 sampai 1440 menit.");
        }

        var deliveryFeeRule = FulfillmentFees.Normalize(request.DeliveryFeeRule, "Pesan Antar");
        var pickupFeeRule = FulfillmentFees.Normalize(request.PickupFeeRule, "Ambil Sendiri");

        var serviceFeeType = request.ServiceFeeType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!ServiceFeeTypes.All.Contains(serviceFeeType))
        {
            throw new InvalidSettingException("Jenis biaya layanan harus PERCENT (persen) atau FIXED (nominal).");
        }

        if (serviceFeeType == ServiceFeeTypes.Percent && !IsValidPercent(request.ServiceFeeValue))
        {
            throw new InvalidSettingException("Biaya layanan (persen) harus antara 0 sampai 100, maksimal 2 angka desimal.");
        }

        if (serviceFeeType == ServiceFeeTypes.Fixed &&
            (request.ServiceFeeValue < 0 || request.ServiceFeeValue > 1_000_000 || request.ServiceFeeValue != decimal.Truncate(request.ServiceFeeValue)))
        {
            throw new InvalidSettingException("Biaya layanan (nominal) harus bilangan bulat Rupiah antara 0 sampai 1.000.000.");
        }

        if (!IsValidPercent(request.TaxPercent))
        {
            throw new InvalidSettingException("Pajak harus antara 0 sampai 100 persen, maksimal 2 angka desimal.");
        }

        var values = new List<AppSetting>
        {
            New(AppSettingKeys.OrderCutoffDaysBefore, request.CutoffDaysBefore.ToString(CultureInfo.InvariantCulture), AppSettingValueTypes.Int,
                "Batas pemesanan: berapa hari sebelum tanggal kirim (H-N)"),
            New(AppSettingKeys.OrderCutoffTime, cutoffTime.ToString(TimeFormat, CultureInfo.InvariantCulture), AppSettingValueTypes.Time,
                "Batas pemesanan: jam cutoff (format HH:mm) pada hari H-N"),
            New(AppSettingKeys.OrderTimezone, timeZoneId, AppSettingValueTypes.String,
                "Zona waktu acuan cutoff dan tanggal kirim"),
            New(AppSettingKeys.PaymentTimeoutMinutes, request.PaymentTimeoutMinutes.ToString(CultureInfo.InvariantCulture), AppSettingValueTypes.Int,
                "Batas waktu pembayaran QRIS dalam menit"),
            New(AppSettingKeys.DeliveryFeeRule, FulfillmentFees.Serialize(deliveryFeeRule), AppSettingValueTypes.String,
                "Aturan ongkir Pesan Antar (JSON: mode FREE/FIXED/PER_DAY, amount, tiers minDays/perDay)"),
            New(AppSettingKeys.PickupFeeRule, FulfillmentFees.Serialize(pickupFeeRule), AppSettingValueTypes.String,
                "Aturan biaya Ambil Sendiri (JSON: mode FREE/FIXED/PER_DAY, amount, tiers minDays/perDay)"),
            New(AppSettingKeys.ServiceFeeType, serviceFeeType, AppSettingValueTypes.String,
                "Jenis biaya layanan: PERCENT (persen dari subtotal - diskon) atau FIXED (Rupiah per pesanan)"),
            New(AppSettingKeys.ServiceFeeValue, request.ServiceFeeValue.ToString("0.##", CultureInfo.InvariantCulture), AppSettingValueTypes.Decimal,
                "Besar biaya layanan: persen (maks 2 desimal) atau Rupiah, sesuai SERVICE_FEE_TYPE"),
            New(AppSettingKeys.TaxPercent, request.TaxPercent.ToString("0.##", CultureInfo.InvariantCulture), AppSettingValueTypes.Decimal,
                "Pajak (persen, maks 2 desimal) dari subtotal - diskon + biaya layanan")
        };

        await settingRepository.UpsertManyAsync(values, updatedBy);
        return await GetOrderSettingsAsync();
    }

    public async Task<OrderCutoffInfoDto> GetOrderCutoffInfoAsync()
    {
        var settings = await GetEffectiveOrderSettingsAsync();
        var nowUtc = DateTime.UtcNow;

        return new OrderCutoffInfoDto(
            CutoffDaysBefore: settings.CutoffDaysBefore,
            CutoffTime: settings.CutoffTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            TimeZone: settings.TimeZoneId,
            ServerTimeUtc: nowUtc,
            EarliestDeliveryDate: settings.GetEarliestDeliveryDate(nowUtc),
            LatestDeliveryDate: settings.GetLocalToday(nowUtc).AddDays(OrderLimits.MaxAdvanceDays),
            DeliveryFeeRule: settings.DeliveryFeeRule,
            PickupFeeRule: settings.PickupFeeRule,
            ServiceFeeType: settings.ServiceFeeType,
            ServiceFeeValue: settings.ServiceFeeValue,
            TaxPercent: settings.TaxPercent
        );
    }

    private static OrderSettingsDto ToDto(OrderSettings s) => new(
        CutoffDaysBefore: s.CutoffDaysBefore,
        CutoffTime: s.CutoffTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
        TimeZone: s.TimeZoneId,
        PaymentTimeoutMinutes: s.PaymentTimeoutMinutes,
        DeliveryFeeRule: s.DeliveryFeeRule,
        PickupFeeRule: s.PickupFeeRule,
        ServiceFeeType: s.ServiceFeeType,
        ServiceFeeValue: s.ServiceFeeValue,
        TaxPercent: s.TaxPercent,
        UpdatedAt: s.UpdatedAt
    );

    private static AppSetting New(string key, string value, string valueType, string description) => new()
    {
        Key = key,
        Value = value,
        ValueType = valueType,
        Description = description
    };
}
