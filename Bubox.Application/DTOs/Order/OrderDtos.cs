namespace Bubox.Application.DTOs.Order;

// ==========================================
// Request
// ==========================================

public record CreateOrderRequest(
    string FulfillmentType,
    Guid? AddressId,
    string? Notes,
    List<CreateOrderDayRequest> Days,
    // Opsional. Diskon dihitung ulang di server; nilai dari client tidak dipakai.
    string? VoucherCode = null,
    // Wajib untuk PICKUP: outlet tempat ambil (harus aktif)
    Guid? OutletId = null
);

public record CreateOrderDayRequest(
    DateOnly DeliveryDate,
    List<CreateOrderItemRequest> Items
);

public record CreateOrderItemRequest(
    Guid MenuId,
    int Qty
);

public record CancelOrderRequest(string? Reason);

public record AdminCancelRequest(string Reason);

public record UpdateDayStatusRequest(string StatusCode);

public record OrderSearchRequest(
    string? Status = null,
    DateOnly? DeliveryDate = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
);

// ==========================================
// Response
// ==========================================

public record QrisPaymentDto(
    string? QrString,
    string? QrUrl,
    string? Acquirer,
    string TransactionStatus,
    decimal GrossAmount,
    DateTime? ExpiresAt,
    DateTime? PaidAt
);

public record CreateOrderResponse(
    Guid OrderId,
    string OrderNumber,
    string StatusCode,
    decimal GrandTotal,
    DateTime PaymentDueAt,
    QrisPaymentDto Payment
);

public record AddressSnapshotDto(
    Guid AddressId,
    string Label,
    string RecipientName,
    string PhoneNumber,
    string FullAddress,
    string Subdistrict,
    string City,
    string Province,
    string PostalCode,
    string? Notes,
    double? Latitude,
    double? Longitude,
    // Kode wilayah rf_wilayah (paling spesifik yang diketahui); null pada pesanan lama
    string? WilayahCode = null
);

/// <summary>Salinan data outlet saat pesanan PICKUP dibuat.</summary>
public record OutletSnapshotDto(
    Guid OutletId,
    string Name,
    string FullAddress,
    string WilayahPath,
    string? PickupHours,
    string? WhatsappNumber,
    double? Latitude,
    double? Longitude
);

public record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string StatusCode,
    string FulfillmentType,
    decimal GrandTotal,
    int DaysCount,
    DateOnly? FirstDeliveryDate,
    DateOnly? LastDeliveryDate,
    DateTime PaymentDueAt,
    DateTime CreatedAt,
    string CustomerName,
    string CustomerEmail,
    IReadOnlyList<DateOnly> DeliveryDates,
    int ItemLinesCount,
    Guid? FirstMenuId,
    string? FirstMenuName
);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public record OrderItemDto(
    Guid Id,
    Guid MenuId,
    string MenuName,
    string CategoryCode,
    string CategoryName,
    decimal UnitPrice,
    int Qty,
    decimal LineTotal,
    /// <summary>Path relatif gambar menu (berversi), null bila menu tanpa gambar.</summary>
    string? ImageUrl
);

public record OrderDayDto(
    Guid Id,
    DateOnly DeliveryDate,
    int DayOfWeek,
    string DayName,
    string StatusCode,
    decimal DaySubtotal,
    bool RequiresManualRefund,
    string? CancelReason,
    DateTime? CancelledAt,
    IReadOnlyList<OrderItemDto> Items
);

public record OrderStatusHistoryDto(
    string Scope,
    Guid EntityId,
    string? FromCode,
    string ToCode,
    string ActorRole,
    string? Reason,
    DateTime CreatedAt
);

public record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    string StatusCode,
    string FulfillmentType,
    AddressSnapshotDto? Address,
    string? Notes,
    decimal Subtotal,
    decimal DiscountAmount,
    string? VoucherCode,
    decimal DeliveryFee,
    decimal GrandTotal,
    DateTime PaymentDueAt,
    DateTime? PaidAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    string? CancelReason,
    DateTime CreatedAt,
    QrisPaymentDto? Payment,
    IReadOnlyList<OrderDayDto> Days,
    IReadOnlyList<OrderStatusHistoryDto> Histories,
    // PICKUP: outlet tempat ambil (null pada pesanan PICKUP lama sebelum ada outlet)
    OutletSnapshotDto? Outlet = null
);

public record ProductionMenuSummaryDto(
    Guid MenuId,
    string MenuName,
    string CategoryName,
    int TotalQty
);

public record DailyOrderDayDto(
    Guid OrderDayId,
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string FulfillmentType,
    string StatusCode,
    AddressSnapshotDto? Address,
    string? Notes,
    IReadOnlyList<OrderItemDto> Items,
    // Pengantaran (DELIVERY saja): zona & driver efektif. AssignmentSource: DISPATCHED | MANUAL | ZONE | UNASSIGNED
    string? ZoneName = null,
    Guid? DriverUserId = null,
    string? DriverName = null,
    string? AssignmentSource = null,
    // Pengambilan (PICKUP saja): outlet tempat ambil
    OutletSnapshotDto? Outlet = null
);

public record DailyProductionDto(
    DateOnly Date,
    int TotalOrders,
    int TotalPortions,
    IReadOnlyList<ProductionMenuSummaryDto> Menus,
    IReadOnlyList<DailyOrderDayDto> Orders
);

public record DailyDeliveriesDto(
    DateOnly Date,
    IReadOnlyList<DailyOrderDayDto> Deliveries
);

/// <summary>
/// Respons GET /Order/GetPaymentStatus: hanya status, untuk di-poll tiap beberapa detik
/// oleh halaman pembayaran. Detail lengkap diambil sekali lewat GetDetailOrder.
/// </summary>
public record OrderPaymentStatusDto(
    Guid OrderId,
    string StatusCode,
    DateTime PaymentDueAt,
    DateTime? PaidAt,
    string? CancelReason
);
