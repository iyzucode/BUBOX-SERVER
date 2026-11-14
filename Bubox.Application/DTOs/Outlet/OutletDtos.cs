namespace Bubox.Application.DTOs.Outlet;

public record UpsertOutletRequest(
    string Name,
    string FullAddress,
    /// <summary>Kode kelurahan/desa rf_wilayah (13 karakter).</summary>
    string WilayahCode,
    /// <summary>Jam ambil mulai "HH:mm" (WIB). Isi bersama PickupEndTime, atau kosongkan keduanya.</summary>
    string? PickupStartTime,
    string? PickupEndTime,
    string? WhatsappNumber,
    double? Latitude,
    double? Longitude,
    bool IsActive
);

public record SetOutletActiveRequest(bool IsActive);

public record OutletDto(
    Guid Id,
    string Name,
    string FullAddress,
    string WilayahCode,
    /// <summary>"Kelurahan, Kecamatan, Kab/Kota, Provinsi".</summary>
    string WilayahPath,
    /// <summary>"HH:mm" atau null.</summary>
    string? PickupStartTime,
    string? PickupEndTime,
    /// <summary>Teks tampilan, mis. "06.30 - 09.00 WIB"; null bila jam ambil tidak diatur.</summary>
    string? PickupHours,
    string? WhatsappNumber,
    double? Latitude,
    double? Longitude,
    int SortOrder,
    bool IsActive,
    /// <summary>Hanya terisi untuk admin: jumlah pesanan yang memakai outlet ini.</summary>
    int? OrderCount = null
);

/// <summary>Pilihan outlet di checkout beserta outlet yang terakhir dipakai customer.</summary>
public record AvailableOutletsDto(
    IReadOnlyList<OutletDto> Outlets,
    Guid? LastUsedOutletId
);
