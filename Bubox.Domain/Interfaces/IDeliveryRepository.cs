using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

/// <summary>
/// Area pengantaran, zona, dan driver. Untuk satu kode wilayah, aturan yang berlaku adalah yang
/// paling spesifik di antara kode itu dan induk-induknya (provinsi 2, kab/kota 5, kecamatan 8,
/// kelurahan 13 karakter).
/// </summary>
public interface IDeliveryRepository
{
    // ---- Master wilayah ----
    Task<WilayahNode?> GetWilayahAsync(string code);

    /// <summary>Anak langsung sebuah wilayah; <paramref name="parentCode"/> null = daftar provinsi.</summary>
    Task<IEnumerable<WilayahNode>> GetChildrenAsync(string? parentCode);

    Task<IEnumerable<WilayahNode>> SearchWilayahAsync(string query, int limit);
    /// <summary>Kelurahan/desa yang memakai kode pos tsb (satu kode pos bisa mencakup banyak kelurahan).</summary>
    Task<IEnumerable<WilayahNode>> SearchByPostalCodeAsync(string postalCode, int limit);
    Task<IEnumerable<WilayahNode>> GetWilayahByCodesAsync(IReadOnlyCollection<string> codes);

    // ---- Aturan ----
    /// <summary>Aturan cakupan paling spesifik untuk kode ini, atau null bila tidak ada aturan sama sekali.</summary>
    Task<CoverageRule?> GetEffectiveCoverageAsync(string wilayahCode);

    /// <summary>Aturan cakupan yang tersimpan tepat untuk kode-kode ini (bukan hasil turunan).</summary>
    Task<IEnumerable<CoverageRule>> GetCoverageRulesAsync(IReadOnlyCollection<string> codes);

    /// <summary>Pemetaan zona yang tersimpan tepat untuk kode-kode ini.</summary>
    Task<IEnumerable<ZoneAreaRule>> GetZoneAreaRulesAsync(IReadOnlyCollection<string> codes);

    /// <summary>Jumlah aturan cakupan & zona di bawah tiap kode (keturunan, tidak termasuk kode itu sendiri).</summary>
    Task<IEnumerable<DescendantRuleCount>> CountDescendantRulesAsync(IReadOnlyCollection<string> codes);

    /// <summary>Simpan aturan cakupan; <paramref name="isCovered"/> null = hapus aturan (ikut induk).</summary>
    Task SetCoverageAsync(string wilayahCode, bool? isCovered, Guid updatedBy, DateTime nowUtc);

    /// <summary>Petakan wilayah ke zona; <paramref name="zoneId"/> null = hapus pemetaan (ikut induk).</summary>
    Task SetZoneAreaAsync(string wilayahCode, Guid? zoneId, Guid updatedBy, DateTime nowUtc);

    // ---- Zona ----
    Task<IEnumerable<DeliveryZone>> GetZonesAsync();
    /// <summary>Kode wilayah yang dipetakan langsung ke zona (bukan turunannya).</summary>
    Task<IEnumerable<string>> GetZoneAreaCodesAsync(Guid zoneId);
    /// <summary>Hapus zona hanya bila tidak punya wilayah & driver aktif. false = tidak dihapus.</summary>
    Task<bool> DeleteZoneIfEmptyAsync(Guid zoneId);
    Task<DeliveryZone?> GetZoneAsync(Guid id);
    Task<bool> ZoneCodeExistsAsync(string code, Guid? excludeId);
    Task CreateZoneAsync(DeliveryZone zone, Guid createdBy);
    Task UpdateZoneAsync(DeliveryZone zone, Guid updatedBy);

    /// <summary>Ganti driver aktif zona (driver lama ditutup sebagai riwayat). null = kosongkan zona.</summary>
    Task SetZoneDriverAsync(Guid zoneId, Guid? driverUserId, Guid changedBy, DateTime nowUtc);

    Task<IEnumerable<ZoneDriverHistoryRow>> GetZoneDriverHistoryAsync(Guid zoneId);

    // ---- Driver ----
    Task<IEnumerable<DriverRow>> GetDriversAsync();
    Task<bool> IsDriverAsync(Guid userId);
    Task<IEnumerable<UserSearchRow>> SearchUsersAsync(string query, int limit);
    Task GrantDriverRoleAsync(Guid userId);

    /// <summary>
    /// Cabut peran Driver: zona yang dipegang dikosongkan & penugasan manual pada kiriman yang belum
    /// berangkat dihapus (kiriman itu kembali mengikuti zona).
    /// </summary>
    Task RevokeDriverRoleAsync(Guid userId, Guid changedBy, DateTime nowUtc);

    // ---- Penugasan per hari kirim ----
    Task<IEnumerable<DayAssignment>> GetDayAssignmentsAsync(IReadOnlyCollection<Guid> orderDayIds);

    /// <summary>
    /// Penugasan manual untuk hari kirim yang belum berangkat. <paramref name="driverUserId"/> null =
    /// hapus penugasan manual (kembali ikut zona). Mengembalikan jumlah hari kirim yang diubah.
    /// </summary>
    Task<int> SetDayDriverOverrideAsync(
        IReadOnlyCollection<Guid> orderDayIds, Guid? driverUserId, string? reason, Guid changedBy, DateTime nowUtc);
}
