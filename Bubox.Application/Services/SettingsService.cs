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
            DeliveryFee = decimal.TryParse(Get(AppSettingKeys.DeliveryFee), NumberStyles.Number, CultureInfo.InvariantCulture, out var fee) && fee >= 0
                ? fee
                : OrderSettings.DefaultDeliveryFee,
            UpdatedAt = settings.Count == 0 ? null : settings.Values.Max(s => s.UpdatedAt)
        };
    }

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

        if (request.DeliveryFee < 0 || request.DeliveryFee > 1_000_000 || request.DeliveryFee != decimal.Truncate(request.DeliveryFee))
        {
            throw new InvalidSettingException("Ongkir harus bilangan bulat Rupiah antara 0 sampai 1.000.000.");
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
            New(AppSettingKeys.DeliveryFee, request.DeliveryFee.ToString("0", CultureInfo.InvariantCulture), AppSettingValueTypes.Decimal,
                "Ongkos kirim per order (Rupiah)")
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
            DeliveryFee: settings.DeliveryFee
        );
    }

    private static OrderSettingsDto ToDto(OrderSettings s) => new(
        CutoffDaysBefore: s.CutoffDaysBefore,
        CutoffTime: s.CutoffTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
        TimeZone: s.TimeZoneId,
        PaymentTimeoutMinutes: s.PaymentTimeoutMinutes,
        DeliveryFee: s.DeliveryFee,
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
