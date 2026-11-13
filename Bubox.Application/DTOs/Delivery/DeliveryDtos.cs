namespace Bubox.Application.DTOs.Delivery;

/// <summary>
/// Satu wilayah di pohon admin, lengkap dengan aturan sendiri & aturan efektif (turunan dari induk).
/// Level: 1 provinsi, 2 kab/kota, 3 kecamatan, 4 kelurahan/desa.
/// </summary>
public record AreaNodeDto(
    string Code,
    string Name,
    int Level,
    bool HasChildren,
    // Aturan cakupan yang disimpan tepat di wilayah ini (null = ikut induk)
    bool? CoverageRule,
    // Hasil akhir: terjangkau atau tidak (tanpa aturan sama sekali = tidak terjangkau)
    bool IsCovered,
    // Wilayah asal aturan cakupan efektif (null bila tidak ada aturan)
    string? CoverageSourceCode,
    string? CoverageSourceName,
    // Zona yang dipetakan tepat di wilayah ini (null = ikut induk)
    Guid? ZoneRuleId,
    // Zona efektif (aturan paling spesifik; zona nonaktif tetap ditampilkan agar terlihat)
    Guid? ZoneId,
    string? ZoneName,
    bool ZoneIsActive,
    string? ZoneSourceCode,
    string? ZoneSourceName,
    // Driver aktif zona efektif
    string? DriverName,
    // Jumlah aturan di bawah wilayah ini (pengecualian di level lebih rendah)
    int CoverageRulesBelow,
    int ZoneRulesBelow,
    // Peringatan: terjangkau tapi tidak ada zona aktif / zona tanpa driver
    string? Warning
);

public record AreaChildrenDto(
    AreaNodeDto? Parent,
    IReadOnlyList<AreaCrumbDto> Breadcrumb,
    IReadOnlyList<AreaNodeDto> Children
);

public record AreaCrumbDto(string Code, string Name);

/// <summary>Hasil cari wilayah. ZoneId/ZoneName = zona yang dipetakan tepat di wilayah ini (bukan warisan induk).</summary>
public record WilayahSearchItemDto(
    string Code,
    string Name,
    int Level,
    string Path,
    string? PostalCode = null,
    Guid? ZoneId = null,
    string? ZoneName = null
);

/// <summary>Wilayah yang dipetakan langsung ke sebuah zona (baca-saja; pemetaan diatur dari sisi wilayah).</summary>
public record ZoneAreaDto(string Code, string Name, int Level, string Path, bool IsCovered);

public record SetCoverageRequest(string WilayahCode, bool? IsCovered);

public record SetZoneAreaRequest(string WilayahCode, Guid? ZoneId);

public record DeliveryZoneDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    int AreaCount,
    Guid? DriverUserId,
    string? DriverName,
    string? DriverEmail
);

public record UpsertZoneRequest(string Code, string Name, string? Description, bool IsActive);

public record SetZoneDriverRequest(Guid? DriverUserId);

public record ZoneDriverHistoryDto(Guid DriverUserId, string DriverName, DateTime AssignedAt, DateTime? UnassignedAt);

public record DriverDto(
    Guid UserId,
    string FullName,
    string Email,
    string? Username,
    string? PhoneNumber,
    IReadOnlyList<string> Zones
);

public record UserSearchDto(Guid UserId, string FullName, string Email, string? Username, bool IsDriver);

/// <summary>Penugasan manual driver untuk satu/lebih hari kirim. DriverUserId null = kembali ikut zona.</summary>
public record SetDayDriverRequest(List<Guid> OrderDayIds, Guid? DriverUserId, string? Reason);

public record SetDayDriverResponse(int Updated);
