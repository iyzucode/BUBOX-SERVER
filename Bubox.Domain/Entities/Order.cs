namespace Bubox.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string FulfillmentType { get; set; } = string.Empty;
    public Guid? AddressId { get; set; }
    public string? AddressSnapshot { get; set; } // JSON
    /// <summary>Kode wilayah alamat antar (rf_wilayah, paling spesifik yang diketahui). Dasar zona & driver.</summary>
    public string? DeliveryWilayahCode { get; set; }
    /// <summary>Outlet tempat ambil (PICKUP saja).</summary>
    public Guid? OutletId { get; set; }
    public string? OutletSnapshot { get; set; } // JSON
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Kode voucher yang dipakai (snapshot), null bila tanpa voucher.</summary>
    public string? VoucherCode { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal ServiceFee { get; set; }
    /// <summary>Tarif biaya layanan bila berupa persen (snapshot); null = nominal tetap.</summary>
    public decimal? ServiceFeePercent { get; set; }
    public decimal TaxAmount { get; set; }
    /// <summary>Tarif pajak saat pesanan dibuat (snapshot).</summary>
    public decimal TaxPercent { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public DateTime PaymentDueAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderDay
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public DateTime DeliveryDate { get; set; } // DATE (tanpa jam)
    public int DayOfWeek { get; set; } // 1: Senin ... 7: Minggu
    public string StatusCode { get; set; } = string.Empty;
    public decimal DaySubtotal { get; set; }
    public bool RequiresManualRefund { get; set; }
    public string? CancelReason { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderDayId { get; set; }
    public Guid MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Qty { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Waktu update menu bila menu punya gambar (untuk URL berversi); null = tanpa gambar. Tidak disimpan.</summary>
    public DateTime? MenuImageUpdatedAt { get; set; }
}

public class OrderStatusHistory
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? FromCode { get; set; }
    public string ToCode { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderStatusTransition
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string FromCode { get; set; } = string.Empty;
    public string ToCode { get; set; } = string.Empty;
    public string[] AllowedRoles { get; set; } = [];
}
