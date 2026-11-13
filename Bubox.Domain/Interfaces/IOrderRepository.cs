using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

/// <summary>Ringkasan order untuk daftar (customer & admin).</summary>
public class OrderListItem
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string FulfillmentType { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public DateTime PaymentDueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DaysCount { get; set; }
    public DateTime? FirstDeliveryDate { get; set; }
    public DateTime? LastDeliveryDate { get; set; }
    public string[]? DeliveryDates { get; set; } // yyyy-MM-dd, urut naik
    public int ItemLinesCount { get; set; } // jumlah baris item (menu × hari)
    public Guid? FirstMenuId { get; set; }
    public string? FirstMenuName { get; set; }
    public int TotalCount { get; set; } // dipakai untuk paging (COUNT(*) OVER())
}

/// <summary>Baris jadwal harian (dapur / pengiriman).</summary>
public class OrderDayScheduleRow
{
    public Guid OrderDayId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime DeliveryDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string FulfillmentType { get; set; } = string.Empty;
    public string? AddressSnapshot { get; set; }
    public string? Notes { get; set; }
}

public class OrderSearchFilter
{
    public string? StatusCode { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? CreatedFromUtc { get; set; }
    public DateTime? CreatedToUtc { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Status ringan untuk polling halaman pembayaran (tanpa hari, item, histori).</summary>
public class OrderPaymentStatus
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime PaymentDueAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? CancelReason { get; set; }
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task<OrderPaymentStatus?> GetPaymentStatusAsync(Guid id);
    Task<IEnumerable<OrderListItem>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<OrderListItem>> SearchAsync(OrderSearchFilter filter);
    Task<IEnumerable<OrderDay>> GetDaysByOrderIdAsync(Guid orderId);
    Task<OrderDay?> GetDayByIdAsync(Guid dayId);
    Task<IEnumerable<OrderItem>> GetItemsByDayIdsAsync(IReadOnlyCollection<Guid> dayIds);
    Task<IEnumerable<OrderStatusHistory>> GetHistoriesAsync(Guid orderId);
    Task<IEnumerable<OrderDayScheduleRow>> GetDaysByDeliveryDateAsync(DateTime deliveryDate, IReadOnlyCollection<string> statusCodes, string? fulfillmentType);
    Task<IEnumerable<Guid>> GetOverdueAwaitingPaymentIdsAsync(DateTime dueBeforeUtc, int limit);
}

public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId);
    Task<Payment?> GetByProviderOrderIdAsync(string providerOrderId);
    Task CreateAsync(Payment payment);
    Task SaveNotificationAsync(PaymentNotification notification);
}

public interface IOrderStatusTransitionRepository
{
    Task<IEnumerable<OrderStatusTransition>> GetAllAsync();
}

public interface IAppSettingRepository
{
    Task<IEnumerable<AppSetting>> GetAllAsync();
    Task UpsertManyAsync(IReadOnlyCollection<AppSetting> settings, Guid updatedBy);
}

/// <summary>
/// Membuka satu transaksi database untuk perubahan order.
/// Semua perubahan status pada satu order wajib lewat sini, setelah order dikunci (SELECT ... FOR UPDATE).
/// </summary>
public interface IOrderUnitOfWorkFactory
{
    Task<IOrderUnitOfWork> BeginAsync();
}

public enum VoucherRedemptionResult
{
    Recorded,
    /// <summary>Pengguna sudah punya pemakaian aktif untuk voucher ini.</summary>
    AlreadyUsed,
    /// <summary>Kuota total voucher sudah habis.</summary>
    QuotaExhausted
}

public interface IOrderUnitOfWork : IAsyncDisposable
{
    Task<Order?> LockOrderAsync(Guid orderId);
    Task<List<OrderDay>> GetDaysAsync(Guid orderId);
    Task<Payment?> GetPaymentAsync(Guid orderId);

    Task InsertOrderAsync(Order order);
    Task InsertDaysAsync(IEnumerable<OrderDay> days);
    Task InsertItemsAsync(IEnumerable<OrderItem> items);
    Task InsertHistoryAsync(OrderStatusHistory history);

    /// <summary>Update status dengan guard optimistic. False jika status saat ini bukan <paramref name="fromCode"/>.</summary>
    Task<bool> UpdateOrderStatusAsync(Guid orderId, string fromCode, string toCode, DateTime nowUtc, string? cancelReason, Guid? cancelledBy);

    /// <summary>Update status hari dengan guard optimistic. False jika status saat ini bukan <paramref name="fromCode"/>.</summary>
    Task<bool> UpdateDayStatusAsync(Guid dayId, string fromCode, string toCode, DateTime nowUtc, string? cancelReason, Guid? cancelledBy, bool requiresManualRefund);

    Task UpdatePaymentAsync(Payment payment);

    /// <summary>
    /// Catat pemakaian voucher untuk pesanan baru. Baris voucher dikunci dulu (FOR UPDATE) agar kuota
    /// tidak terlampaui oleh pesanan bersamaan; batas 1x per pengguna dijaga index unik di database.
    /// </summary>
    Task<VoucherRedemptionResult> InsertVoucherRedemptionAsync(VoucherRedemption redemption);

    /// <summary>Lepas pemakaian voucher pesanan ini (pesanan ditutup sebelum dibayar), agar bisa dipakai lagi.</summary>
    Task ReleaseVoucherRedemptionAsync(Guid orderId, DateTime nowUtc);

    Task CommitAsync();
}
