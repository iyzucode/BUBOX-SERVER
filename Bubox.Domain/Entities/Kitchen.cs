namespace Bubox.Domain.Entities;

/// <summary>Rilis daftar masak satu tanggal kirim ke dapur (oleh admin, setelah validasi H-1).</summary>
public class KitchenRelease
{
    public DateTime DeliveryDate { get; set; } // DATE
    public DateTime ReleasedAt { get; set; }
    public Guid? ReleasedBy { get; set; }
    public int DayCount { get; set; }
    public int Portions { get; set; }

    // Hasil join (hanya dibaca)
    public string? ReleasedByName { get; set; }
}
