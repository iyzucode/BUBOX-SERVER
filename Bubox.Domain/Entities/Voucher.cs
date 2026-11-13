namespace Bubox.Domain.Entities;

public class Voucher
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = string.Empty; // VoucherDiscountTypes
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinSubtotal { get; set; }
    public string[] PaymentMethods { get; set; } = [];
    /// <summary>Kuota total pemakaian (semua pengguna). Null = tanpa batas.</summary>
    public int? UsageLimit { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsListed { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Jumlah pemakaian aktif (belum dilepas oleh pesanan yang batal sebelum dibayar).</summary>
    public int RedemptionCount { get; set; }

    public bool IsQuotaExhausted => UsageLimit is { } limit && RedemptionCount >= limit;
}

public class VoucherRedemption
{
    public Guid Id { get; set; }
    public Guid VoucherId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
