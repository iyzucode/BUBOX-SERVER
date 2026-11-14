namespace Bubox.Domain.Entities;

/// <summary>Outlet tempat customer mengambil pesanan (PICKUP).</summary>
public class Outlet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    /// <summary>Kode kelurahan/desa rf_wilayah (13 karakter).</summary>
    public string WilayahCode { get; set; } = string.Empty;
    /// <summary>Jam ambil mulai (WIB). Null bersama <see cref="PickupEndTime"/> bila tidak diatur.</summary>
    public TimeSpan? PickupStartTime { get; set; }
    public TimeSpan? PickupEndTime { get; set; }
    public string? WhatsappNumber { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Hasil join (hanya dibaca)
    /// <summary>"Kelurahan, Kecamatan, Kab/Kota, Provinsi" dari rf_wilayah.</summary>
    public string WilayahPath { get; set; } = string.Empty;
    /// <summary>Jumlah pesanan yang memakai outlet ini (penentu boleh dihapus).</summary>
    public int OrderCount { get; set; }
}
