using System.Globalization;
using System.Text.RegularExpressions;
using Bubox.Application.DTOs.Outlet;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public partial class OutletService(
    IOutletRepository outletRepository,
    IDeliveryRepository deliveryRepository) : IOutletService
{
    [GeneratedRegex(@"^\d{2}\.\d{2}\.\d{2}\.\d{4}$")]
    private static partial Regex VillageCodePattern();

    /// <summary>Nomor HP Indonesia: 08xx / 628xx / +628xx, 10-15 digit.</summary>
    [GeneratedRegex(@"^(?:\+?62|0)8\d{7,12}$")]
    private static partial Regex WhatsappPattern();

    [GeneratedRegex(@"[\s\-().]")]
    private static partial Regex PhoneSeparators();

    public async Task<AvailableOutletsDto> GetAvailableAsync(Guid? userId)
    {
        var outlets = (await outletRepository.GetActiveAsync()).Select(o => ToDto(o, includeUsage: false)).ToList();
        Guid? lastUsed = null;
        if (userId is { } id)
        {
            var candidate = await outletRepository.GetLastUsedOutletIdAsync(id);
            // Hanya bila outlet itu masih aktif
            lastUsed = outlets.Any(o => o.Id == candidate) ? candidate : null;
        }
        return new AvailableOutletsDto(outlets, lastUsed);
    }

    public async Task<IReadOnlyList<OutletDto>> GetAllAsync() =>
        (await outletRepository.GetAllAsync()).Select(o => ToDto(o, includeUsage: true)).ToList();

    public async Task<OutletDto> CreateAsync(UpsertOutletRequest request, Guid adminId)
    {
        var nowUtc = DateTime.UtcNow;
        var outlet = new Outlet
        {
            Id = Guid.NewGuid(),
            CreatedBy = adminId,
            CreatedAt = nowUtc
        };
        await ApplyAsync(outlet, request, adminId, nowUtc, excludeId: null);
        // Outlet baru tampil paling bawah. Urutan diubah lewat fitur atur urutan (menyusul), bukan form.
        outlet.SortOrder = await outletRepository.GetNextSortOrderAsync();
        await outletRepository.CreateAsync(outlet);
        return await GetDtoAsync(outlet.Id);
    }

    public async Task<OutletDto> UpdateAsync(Guid id, UpsertOutletRequest request, Guid adminId)
    {
        var outlet = await outletRepository.GetByIdAsync(id) ?? throw new OutletNotFoundException();
        await ApplyAsync(outlet, request, adminId, DateTime.UtcNow, excludeId: id);
        await outletRepository.UpdateAsync(outlet);
        return await GetDtoAsync(id);
    }

    public async Task<OutletDto> SetActiveAsync(Guid id, bool isActive, Guid adminId)
    {
        _ = await outletRepository.GetByIdAsync(id) ?? throw new OutletNotFoundException();
        await outletRepository.SetActiveAsync(id, isActive, adminId, DateTime.UtcNow);
        return await GetDtoAsync(id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var outlet = await outletRepository.GetByIdAsync(id) ?? throw new OutletNotFoundException();
        if (outlet.OrderCount > 0 || !await outletRepository.DeleteIfUnusedAsync(id))
        {
            throw new OutletInUseException();
        }
    }

    // ---- Helpers ----

    private async Task ApplyAsync(Outlet outlet, UpsertOutletRequest request, Guid adminId, DateTime nowUtc, Guid? excludeId)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 3 or > 100)
        {
            throw new InvalidOutletException("Nama outlet 3-100 karakter.");
        }
        if (await outletRepository.NameExistsAsync(name, excludeId))
        {
            throw new OutletNameExistsException();
        }

        var fullAddress = request.FullAddress?.Trim() ?? string.Empty;
        if (fullAddress.Length is < 5 or > 500)
        {
            throw new InvalidOutletException("Alamat lengkap 5-500 karakter.");
        }

        var wilayahCode = request.WilayahCode?.Trim() ?? string.Empty;
        if (!VillageCodePattern().IsMatch(wilayahCode) || await deliveryRepository.GetWilayahAsync(wilayahCode) == null)
        {
            throw new InvalidOutletException("Pilih wilayah outlet sampai kelurahan/desa.");
        }

        var pickupStart = ParseTime(request.PickupStartTime, "mulai");
        var pickupEnd = ParseTime(request.PickupEndTime, "selesai");
        if ((pickupStart is null) != (pickupEnd is null))
        {
            throw new InvalidOutletException("Isi jam ambil mulai dan selesai, atau kosongkan keduanya.");
        }
        if (pickupStart is { } start && pickupEnd is { } end && end <= start)
        {
            throw new InvalidOutletException("Jam ambil selesai harus setelah jam mulai.");
        }

        string? whatsapp = null;
        if (!string.IsNullOrWhiteSpace(request.WhatsappNumber))
        {
            whatsapp = PhoneSeparators().Replace(request.WhatsappNumber, string.Empty);
            if (!WhatsappPattern().IsMatch(whatsapp))
            {
                throw new InvalidOutletException("Nomor WhatsApp tidak valid. Contoh: 081234567890.");
            }
        }

        if ((request.Latitude is null) != (request.Longitude is null))
        {
            throw new InvalidOutletException("Titik peta harus berisi latitude dan longitude.");
        }
        if (request.Latitude is { } lat && (lat < -90 || lat > 90) || request.Longitude is { } lng && (lng < -180 || lng > 180))
        {
            throw new InvalidOutletException("Titik peta tidak valid.");
        }

        outlet.Name = name;
        outlet.FullAddress = fullAddress;
        outlet.WilayahCode = wilayahCode;
        outlet.PickupStartTime = pickupStart;
        outlet.PickupEndTime = pickupEnd;
        outlet.WhatsappNumber = whatsapp;
        outlet.Latitude = request.Latitude;
        outlet.Longitude = request.Longitude;
        outlet.IsActive = request.IsActive;
        outlet.UpdatedBy = adminId;
        outlet.UpdatedAt = nowUtc;
    }

    private static TimeSpan? ParseTime(string? value, string which)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new InvalidOutletException($"Jam ambil {which} tidak valid (format HH:mm).");
        }
        return time.ToTimeSpan();
    }

    private static string? FormatTime(TimeSpan? time) => time?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>Teks jam ambil untuk customer, mis. "06.30 - 09.00 WIB".</summary>
    internal static string? FormatPickupHours(Outlet o) =>
        o.PickupStartTime is { } start && o.PickupEndTime is { } end
            ? $"{ToDotted(start)} - {ToDotted(end)} WIB"
            : null;

    private static string ToDotted(TimeSpan time) => time.ToString(@"hh\.mm", CultureInfo.InvariantCulture);

    private async Task<OutletDto> GetDtoAsync(Guid id) =>
        ToDto(await outletRepository.GetByIdAsync(id) ?? throw new OutletNotFoundException(), includeUsage: true);

    internal static OutletDto ToDto(Outlet o, bool includeUsage) => new(
        Id: o.Id,
        Name: o.Name,
        FullAddress: o.FullAddress,
        WilayahCode: o.WilayahCode,
        WilayahPath: o.WilayahPath,
        PickupStartTime: FormatTime(o.PickupStartTime),
        PickupEndTime: FormatTime(o.PickupEndTime),
        PickupHours: FormatPickupHours(o),
        WhatsappNumber: o.WhatsappNumber,
        Latitude: o.Latitude,
        Longitude: o.Longitude,
        SortOrder: o.SortOrder,
        IsActive: o.IsActive,
        OrderCount: includeUsage ? o.OrderCount : null);
}
