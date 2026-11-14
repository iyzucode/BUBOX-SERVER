namespace Bubox.Application.DTOs.Order;

// ==========================================
// Layar admin Pesanan: daftar per hari kirim & per pesanan
// ==========================================

/// <summary>
/// Filter layar admin Pesanan. Daftar multi-nilai dikirim sebagai teks dipisah koma
/// (mis. Fulfillment = "DELIVERY,PICKUP"; ZoneIds = "&lt;guid&gt;,NONE"). "NONE" = belum ada zona / driver.
/// </summary>
public record AdminOrderBoardRequest(
    /// <summary>DELIVERY = tanggal kirim (default), CREATED = tanggal pesan.</summary>
    string? DateBasis = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    string? Q = null,
    string? Fulfillment = null,
    string? ZoneIds = null,
    /// <summary>Hanya tampilan per hari kirim.</summary>
    string? DriverIds = null,
    string? OutletIds = null,
    string? CategoryCodes = null,
    Guid? MenuId = null,
    /// <summary>Kunci tab status (lihat AdminBoardTabs); default ALL.</summary>
    string? Tab = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = 20
);

public record AdminFacetOptionDto(string Id, string Label, int Count);

public record AdminFacetsDto(
    int DeliveryCount,
    int PickupCount,
    IReadOnlyList<AdminFacetOptionDto> Zones,
    IReadOnlyList<AdminFacetOptionDto> Drivers,
    IReadOnlyList<AdminFacetOptionDto> Outlets,
    IReadOnlyList<AdminFacetOptionDto> Categories
);

public record AdminBoardSummaryDto(
    /// <summary>Jumlah baris (hari kirim atau pesanan) di daftar.</summary>
    int Rows,
    int Orders,
    int Portions,
    /// <summary>Omzet dari pesanan yang sudah dibayar (tanpa yang dibatalkan).</summary>
    decimal PaidRevenue,
    int AwaitingPayment,
    int NeedsAttention
);

/// <summary>Satu hari kirim di tampilan "Per Hari Kirim".</summary>
public record AdminDayRowDto(
    Guid OrderDayId,
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string? CustomerPhone,
    DateOnly DeliveryDate,
    int DayIndex,
    int DayCount,
    string DayStatus,
    string OrderStatus,
    string FulfillmentType,
    DateTime DayUpdatedAt,
    // Menu & porsi
    string? FirstMenuName,
    int MenuCount,
    int Portions,
    string? Notes,
    // Pengantaran (DELIVERY)
    string? RecipientName,
    string? RecipientPhone,
    string? AddressLine,
    Guid? ZoneId,
    string? ZoneName,
    Guid? DriverUserId,
    string? DriverName,
    string? AssignmentSource,
    // Pengambilan (PICKUP)
    Guid? OutletId,
    string? OutletName,
    string? PickupHours,
    // Pembayaran & tindakan
    DateTime PaymentDueAt,
    DateTime? PaidAt,
    bool PaymentNeedsReview,
    bool RequiresManualRefund,
    bool NeedsDriver,
    bool NeedsAttention
);

/// <summary>Satu pesanan di tampilan "Per Pesanan".</summary>
public record AdminOrderRowDto(
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string? CustomerPhone,
    DateTime CreatedAt,
    string OrderStatus,
    string FulfillmentType,
    string? ZoneName,
    string? OutletName,
    DateOnly FirstDeliveryDate,
    DateOnly LastDeliveryDate,
    int DayCount,
    /// <summary>Hari yang sudah diterima / diambil.</summary>
    int DoneDays,
    int CancelledDays,
    int Portions,
    decimal GrandTotal,
    DateTime PaymentDueAt,
    DateTime? PaidAt,
    bool PaymentNeedsReview,
    int RefundPendingDays,
    decimal RefundPendingAmount,
    int NeedsDriverDays,
    bool NeedsAttention
);

public record AdminBoardResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    /// <summary>Jumlah per tab status (setelah filter, sebelum tab).</summary>
    IReadOnlyDictionary<string, int> TabCounts,
    /// <summary>Jumlah per opsi filter, dihitung dari periode + pencarian (sebelum filter lain).</summary>
    AdminFacetsDto Facets,
    AdminBoardSummaryDto Summary,
    /// <summary>Rekap dapur per menu (tampilan per hari kirim; hari yang sudah dibayar & tidak batal).</summary>
    IReadOnlyList<ProductionMenuSummaryDto> Production
);

/// <summary>Penugasan pengantaran satu hari kirim (panel detail admin).</summary>
public record AdminDayAssignmentDto(
    Guid OrderDayId,
    string? ZoneName,
    Guid? DriverUserId,
    string? DriverName,
    string? AssignmentSource
);

/// <summary>Detail pesanan untuk panel samping admin: detail biasa + data pelanggan & penugasan per hari.</summary>
public record AdminOrderDetailDto(
    OrderDetailDto Order,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    bool PaymentNeedsReview,
    string? PaymentReviewNote,
    IReadOnlyList<AdminDayAssignmentDto> Assignments
);
