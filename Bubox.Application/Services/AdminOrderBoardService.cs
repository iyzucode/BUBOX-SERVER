using Bubox.Application.DTOs.Order;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Application.Services.Export;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

/// <summary>Kunci tab status layar admin Pesanan.</summary>
public static class AdminBoardTabs
{
    public const string All = "ALL";
    public const string AwaitingPayment = "AWAITING_PAYMENT";
    public const string Attention = "ATTENTION";
    public const string Cancelled = "CANCELLED";

    // Per hari kirim
    public const string Done = "DONE";
    public static readonly IReadOnlyList<string> DayTabs =
    [
        All, AwaitingPayment, OrderDayStatusCodes.Confirmed, OrderDayStatusCodes.Preparing, OrderDayStatusCodes.Ready,
        OrderDayStatusCodes.OutForDelivery, Done, Cancelled, Attention
    ];

    // Per pesanan
    public static readonly IReadOnlyList<string> OrderTabs =
    [
        All, AwaitingPayment, OrderStatusCodes.Paid, OrderStatusCodes.InProgress, OrderStatusCodes.Completed, Cancelled, Attention
    ];
}

/// <summary>Status bayar pesanan (filter & kolom pembayaran), satu nilai per pesanan.</summary>
public static class AdminPaymentStatuses
{
    public const string Paid = "PAID";
    public const string Awaiting = "AWAITING";
    public const string Review = "REVIEW";
    public const string Expired = "EXPIRED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<(string Code, string Label)> Options =
    [
        (Paid, "Lunas"), (Awaiting, "Menunggu bayar"), (Review, "Perlu ditinjau"),
        (Expired, "Kedaluwarsa"), (Failed, "Gagal bayar"), (Cancelled, "Batal sebelum bayar")
    ];
}

/// <summary>Status refund manual (filter).</summary>
public static class AdminRefundStatuses
{
    public const string Pending = "PENDING";
    public const string Done = "DONE";

    public static readonly IReadOnlyList<(string Code, string Label)> Options =
    [
        (Pending, "Refund tertunda"), (Done, "Sudah direfund")
    ];
}

/// <summary>
/// Layar admin Pesanan: daftar per hari kirim & per pesanan dengan filter bertingkat, tab status, jumlah per opsi
/// filter, ringkasan, rekap dapur, aksi massal, ekspor Excel, dan lembar dapur. Data satu periode diambil sekali
/// dari DB lalu difilter di memori (jumlah pesanan katering per periode kecil), sehingga semua hitungan konsisten.
/// </summary>
public class AdminOrderBoardService(
    IOrderRepository orderRepository,
    ISettingsService settingsService,
    IOrderService orderService,
    IOrderAdminService orderAdminService) : IAdminOrderBoardService
{
    private const string None = "NONE";
    private const int MaxRangeDays = 93;
    private const int MaxBulkItems = 200;
    private static readonly int[] AllowedPageSizes = [10, 20, 50, 100];

    private static readonly HashSet<string> CancelledOrderStatuses =
    [
        OrderStatusCodes.CancelledByCustomer, OrderStatusCodes.CancelledByAdmin, OrderStatusCodes.Expired, OrderStatusCodes.PaymentFailed
    ];

    private static readonly HashSet<string> PaidOrderStatuses =
    [
        OrderStatusCodes.Paid, OrderStatusCodes.InProgress, OrderStatusCodes.Completed
    ];

    private static readonly HashSet<string> CancelledDayStatuses =
    [
        OrderDayStatusCodes.Cancelled, OrderDayStatusCodes.CancelledByAdmin
    ];

    private static readonly HashSet<string> DoneDayStatuses =
    [
        OrderDayStatusCodes.Delivered, OrderDayStatusCodes.PickedUp
    ];

    /// <summary>Hari kirim yang masih perlu driver (sudah dibayar, belum berangkat).</summary>
    private static readonly HashSet<string> NeedsDriverDayStatuses =
    [
        OrderDayStatusCodes.Confirmed, OrderDayStatusCodes.Preparing, OrderDayStatusCodes.Ready
    ];

    // ==========================================
    // Per hari kirim
    // ==========================================

    public async Task<AdminBoardResult<AdminDayRowDto>> SearchDaysAsync(AdminOrderBoardRequest request)
    {
        var list = await BuildDayListAsync(request);
        var (filter, listed) = (list.Filter, list.Listed);

        var summary = new AdminBoardSummaryDto(
            Rows: listed.Count,
            Orders: listed.Select(r => r.Row.OrderId).Distinct().Count(),
            Portions: listed.Where(r => !IsCancelled(r.Row)).Sum(r => r.Portions),
            PaidRevenue: listed.Where(r => PaidOrderStatuses.Contains(r.Row.OrderStatus) && !CancelledDayStatuses.Contains(r.Row.DayStatus))
                .Sum(r => r.Row.DaySubtotal),
            AwaitingPayment: listed.Count(r => InDayTab(r, AdminBoardTabs.AwaitingPayment)),
            NeedsAttention: listed.Count(r => r.NeedsAttention));

        // Rekap dapur: seluruh hari yang lolos filter (bukan hanya tab aktif), sudah dibayar & belum batal
        var production = Production(list.Filtered.Where(r => OrderDayStatusCodes.Production.Contains(r.Row.DayStatus)));

        var attentionDays = list.Filtered.Where(r => r.NeedsAttention).ToList();
        var attention = new AdminAttentionDto(
            Total: attentionDays.Count,
            NeedsDriver: attentionDays.Count(r => r.NeedsDriver),
            RefundPending: attentionDays.Count(r => r.RefundPending),
            PaymentReview: attentionDays.Count(r => r.Row.PaymentNeedsReview),
            Rows: attentionDays.OrderBy(r => r.Row.DeliveryDate).ThenBy(r => r.Row.OrderNumber).Take(MaxBulkItems).Select(ToDayDto).ToList());

        var page = listed.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(ToDayDto).ToList();
        return new AdminBoardResult<AdminDayRowDto>(page, filter.Page, filter.PageSize, listed.Count, list.TabCounts, list.Facets, summary, production, attention);
    }

    // ==========================================
    // Per pesanan
    // ==========================================

    public async Task<AdminBoardResult<AdminOrderRowDto>> SearchOrdersAsync(AdminOrderBoardRequest request)
    {
        var list = await BuildOrderListAsync(request);
        var (filter, listed) = (list.Filter, list.Listed);

        var summary = new AdminBoardSummaryDto(
            Rows: listed.Count,
            Orders: listed.Count,
            Portions: listed.Sum(o => o.Dto.Portions),
            PaidRevenue: listed.Where(o => PaidOrderStatuses.Contains(o.Dto.OrderStatus)).Sum(o => o.Dto.GrandTotal),
            AwaitingPayment: listed.Count(o => o.Dto.OrderStatus == OrderStatusCodes.AwaitingPayment),
            NeedsAttention: listed.Count(o => o.Dto.NeedsAttention));

        var attentionOrders = list.Filtered.Where(o => o.Dto.NeedsAttention).ToList();
        var attention = new AdminAttentionDto(
            Total: attentionOrders.Count,
            NeedsDriver: attentionOrders.Count(o => o.Dto.NeedsDriverDays > 0),
            RefundPending: attentionOrders.Count(o => o.Dto.RefundPendingDays > 0),
            PaymentReview: attentionOrders.Count(o => o.Dto.PaymentNeedsReview),
            Rows: []);

        var page = listed.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(o => o.Dto).ToList();
        return new AdminBoardResult<AdminOrderRowDto>(page, filter.Page, filter.PageSize, listed.Count, list.TabCounts, list.Facets, summary, [], attention);
    }

    // ==========================================
    // Detail (panel samping)
    // ==========================================

    public async Task<AdminOrderDetailDto> GetOrderDetailAsync(Guid orderId, OrderActor actor)
    {
        var detail = await orderService.GetOrderDetailAsync(orderId, actor);
        var rows = (await orderRepository.GetAdminDayRowsAsync(new AdminDayRowQuery { OrderId = orderId, WholeOrders = true })).ToList();
        var first = rows.FirstOrDefault();

        return new AdminOrderDetailDto(
            Order: detail,
            CustomerName: first?.CustomerName ?? string.Empty,
            CustomerEmail: first?.CustomerEmail ?? string.Empty,
            CustomerPhone: first?.CustomerPhone,
            PaymentNeedsReview: first?.PaymentNeedsReview ?? false,
            PaymentReviewNote: first?.PaymentReviewNote,
            Assignments: rows.Select(r => new AdminDayAssignmentDto(
                r.OrderDayId, r.ZoneName, r.DriverUserId, r.DriverName, r.AssignmentSource,
                r.RefundedAt, r.RefundedByName, r.RefundNote)).ToList());
    }

    // ==========================================
    // Aksi massal
    // ==========================================

    public async Task<BulkActionResultDto> BulkUpdateDayStatusAsync(BulkDayStatusRequest request, OrderActor actor)
    {
        var ids = BulkIds(request.OrderDayIds);
        var target = request.StatusCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!OrderDayStatusCodes.ManualProgress.Contains(target))
        {
            throw new InvalidOrderException($"Status '{request.StatusCode}' tidak dapat di-set lewat aksi massal.");
        }

        return await RunBulkAsync(ids, id => orderAdminService.UpdateDayStatusAsync(id, target, actor));
    }

    public async Task<BulkActionResultDto> BulkCancelDaysAsync(BulkCancelDaysRequest request, OrderActor actor)
    {
        var ids = BulkIds(request.OrderDayIds);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOrderException("Alasan pembatalan wajib diisi.");
        }

        return await RunBulkAsync(ids, id => orderAdminService.CancelDayByAdminAsync(id, request.Reason, actor));
    }

    public async Task<BulkActionResultDto> MarkDaysRefundedAsync(MarkDaysRefundedRequest request, Guid adminId)
    {
        var ids = BulkIds(request.OrderDayIds);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > 300)
        {
            throw new InvalidOrderException("Catatan refund maksimal 300 karakter.");
        }

        var updated = (await orderRepository.MarkDaysRefundedAsync(ids, note, adminId, DateTime.UtcNow)).ToHashSet();
        var failed = new List<BulkActionFailureDto>();
        foreach (var id in ids.Where(id => !updated.Contains(id)))
        {
            failed.Add(new BulkActionFailureDto(id, await OrderNumberOfDayAsync(id),
                "Tidak ada refund tertunda di hari kirim ini (sudah ditandai, atau hari ini tidak perlu refund)."));
        }

        return new BulkActionResultDto(updated.Count, failed);
    }

    private static List<Guid> BulkIds(List<Guid>? orderDayIds)
    {
        var ids = (orderDayIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > MaxBulkItems)
        {
            throw new InvalidOrderException($"Pilih 1-{MaxBulkItems} hari kirim.");
        }
        return ids;
    }

    /// <summary>Setiap hari diproses dalam transaksinya sendiri; kegagalan bisnis dicatat, tidak menghentikan yang lain.</summary>
    private async Task<BulkActionResultDto> RunBulkAsync(List<Guid> ids, Func<Guid, Task> action)
    {
        var succeeded = 0;
        var failed = new List<BulkActionFailureDto>();
        foreach (var id in ids)
        {
            try
            {
                await action(id);
                succeeded++;
            }
            catch (Exception ex) when (ex is OrderNotFoundException or InvalidOrderException or InvalidStatusTransitionException
                                           or ForbiddenStatusTransitionException or OrderConcurrencyException or DriverNotAssignedException)
            {
                failed.Add(new BulkActionFailureDto(id, await OrderNumberOfDayAsync(id), ex.Message));
            }
        }
        return new BulkActionResultDto(succeeded, failed);
    }

    private async Task<string?> OrderNumberOfDayAsync(Guid orderDayId)
    {
        var day = await orderRepository.GetDayByIdAsync(orderDayId);
        return day == null ? null : (await orderRepository.GetByIdAsync(day.OrderId))?.OrderNumber;
    }

    // ==========================================
    // Ekspor Excel
    // ==========================================

    public async Task<AdminExportFile> ExportDaysAsync(AdminOrderBoardRequest request)
    {
        var list = await BuildDayListAsync(request);
        var tz = list.Filter.TimeZone;

        var sheet = new XlsxSheet("Per Hari Kirim",
        [
            new("Tanggal kirim", XlsxType.Date, 13),
            new("No. pesanan", Width: 24),
            new("Hari ke", Width: 8),
            new("Pelanggan", Width: 22),
            new("HP pelanggan", Width: 15),
            new("Metode", Width: 13),
            new("Zona", Width: 14),
            new("Driver", Width: 16),
            new("Outlet", Width: 16),
            new("Penerima", Width: 18),
            new("HP penerima", Width: 15),
            new("Alamat", Width: 40),
            new("Menu", Width: 40),
            new("Porsi", XlsxType.Number, 7, Sum: true),
            new("Catatan", Width: 28),
            new("Status hari", Width: 14),
            new("Status bayar", Width: 16),
            new("Voucher", Width: 12),
            new("Subtotal hari (Rp)", XlsxType.Number, 16, Sum: true),
            new("Refund", Width: 22),
            new("Tanggal pesan", XlsxType.DateTime, 17)
        ]);

        foreach (var r in list.Listed)
        {
            var row = r.Row;
            sheet.AddRow(
                OrderMapper.ToDateOnly(row.DeliveryDate),
                row.OrderNumber,
                $"{row.DayIndex}/{row.DayCount}",
                row.CustomerName,
                row.CustomerPhone,
                FulfillmentLabel(row.FulfillmentType),
                row.ZoneName,
                row.DriverName,
                r.Outlet?.Name,
                r.Address?.RecipientName,
                r.Address?.PhoneNumber,
                r.Address == null ? null : $"{r.Address.FullAddress}, {r.Address.Subdistrict}",
                string.Join("; ", r.Items.OrderBy(i => i.MenuName).Select(i => $"{i.MenuName} ×{i.Qty}")),
                r.Portions,
                row.Notes,
                DayStatusLabel(row),
                PaymentLabel(r.PaymentStatus),
                row.VoucherCode,
                row.DaySubtotal,
                RefundLabel(row, tz),
                ToLocal(row.OrderCreatedAt, tz));
        }

        return new AdminExportFile(XlsxWriter.Build(sheet), FileName("pesanan-per-hari", list.Filter));
    }

    public async Task<AdminExportFile> ExportOrdersAsync(AdminOrderBoardRequest request)
    {
        var list = await BuildOrderListAsync(request);
        var tz = list.Filter.TimeZone;

        var sheet = new XlsxSheet("Transaksi",
        [
            new("Tanggal pesan", XlsxType.DateTime, 17),
            new("No. pesanan", Width: 24),
            new("Pelanggan", Width: 22),
            new("Email", Width: 26),
            new("HP", Width: 15),
            new("Metode", Width: 13),
            new("Zona / Outlet", Width: 18),
            new("Kirim mulai", XlsxType.Date, 12),
            new("Kirim sampai", XlsxType.Date, 12),
            new("Jumlah hari", XlsxType.Number, 8),
            new("Porsi", XlsxType.Number, 7, Sum: true),
            new("Subtotal (Rp)", XlsxType.Number, 14, Sum: true),
            new("Voucher", Width: 12),
            new("Diskon (Rp)", XlsxType.Number, 12, Sum: true),
            new("Ongkir (Rp)", XlsxType.Number, 12, Sum: true),
            new("Biaya layanan (Rp)", XlsxType.Number, 14, Sum: true),
            new("Pajak (Rp)", XlsxType.Number, 12, Sum: true),
            new("Total (Rp)", XlsxType.Number, 14, Sum: true),
            new("Status pesanan", Width: 18),
            new("Status bayar", Width: 16),
            new("Dibayar pada", XlsxType.DateTime, 17),
            new("Refund tertunda (Rp)", XlsxType.Number, 16, Sum: true),
            new("Hari direfund", XlsxType.Number, 10)
        ]);

        foreach (var o in list.Listed)
        {
            var dto = o.Dto;
            var first = o.First;
            sheet.AddRow(
                ToLocal(dto.CreatedAt, tz),
                dto.OrderNumber,
                dto.CustomerName,
                first.CustomerEmail,
                dto.CustomerPhone,
                FulfillmentLabel(dto.FulfillmentType),
                dto.FulfillmentType == FulfillmentTypes.Delivery ? dto.ZoneName : dto.OutletName,
                dto.FirstDeliveryDate,
                dto.LastDeliveryDate,
                dto.DayCount,
                dto.Portions,
                first.Subtotal,
                first.VoucherCode,
                first.DiscountAmount,
                first.DeliveryFee,
                first.ServiceFee,
                first.TaxAmount,
                dto.GrandTotal,
                OrderStatusLabel(dto.OrderStatus),
                PaymentLabel(dto.PaymentStatus),
                dto.PaidAt == null ? null : ToLocal(dto.PaidAt.Value, tz),
                dto.RefundPendingAmount,
                dto.RefundedDays);
        }

        return new AdminExportFile(XlsxWriter.Build(sheet), FileName("transaksi", list.Filter));
    }

    // ==========================================
    // Lembar dapur
    // ==========================================

    public async Task<KitchenSheetDto> GetKitchenSheetAsync(AdminOrderBoardRequest request)
    {
        var list = await BuildDayListAsync(request);
        // Hanya yang benar-benar dimasak: sudah dibayar & tidak batal (mengikuti filter dan tab aktif)
        var days = list.Listed
            .Where(r => OrderDayStatusCodes.Production.Contains(r.Row.DayStatus))
            .OrderBy(r => r.Row.DeliveryDate)
            .ThenBy(r => r.Row.FulfillmentType)
            .ThenBy(r => r.Row.ZoneName ?? r.Outlet?.Name ?? string.Empty)
            .ThenBy(r => r.Row.DriverName ?? string.Empty)
            .ThenBy(r => r.Row.OrderNumber)
            .ToList();

        var dates = days.GroupBy(r => r.Row.DeliveryDate)
            .Select(g => new KitchenSheetDateDto(OrderMapper.ToDateOnly(g.Key), g.Sum(r => r.Portions), Production(g)))
            .ToList();

        return new KitchenSheetDto(
            DateFrom: list.Filter.From,
            DateTo: list.Filter.To,
            GeneratedAt: DateTime.UtcNow,
            Portions: days.Sum(r => r.Portions),
            Menus: Production(days),
            Dates: dates,
            Days: days.Select(r => new KitchenSheetDayDto(
                OrderDayId: r.Row.OrderDayId,
                DeliveryDate: OrderMapper.ToDateOnly(r.Row.DeliveryDate),
                OrderNumber: r.Row.OrderNumber,
                DayIndex: r.Row.DayIndex,
                DayCount: r.Row.DayCount,
                CustomerName: r.Row.CustomerName,
                FulfillmentType: r.Row.FulfillmentType,
                RecipientName: r.Address?.RecipientName,
                RecipientPhone: r.Address?.PhoneNumber,
                AddressLine: r.Address == null ? null : $"{r.Address.FullAddress}, {r.Address.Subdistrict}",
                ZoneName: r.Row.ZoneName,
                DriverName: r.Row.DriverName,
                OutletName: r.Outlet?.Name,
                PickupHours: r.Outlet?.PickupHours,
                Notes: r.Row.Notes,
                DayStatus: r.Row.DayStatus,
                Portions: r.Portions,
                Items: r.Items.OrderBy(i => i.CategoryName).ThenBy(i => i.MenuName)
                    .Select(i => new KitchenSheetItemDto(i.MenuName, i.CategoryName, i.Qty)).ToList())).ToList());
    }

    // ==========================================
    // Aktivitas (indikator pesanan baru)
    // ==========================================

    public async Task<BoardActivityDto> GetActivityAsync(DateTime sinceUtc)
    {
        var now = DateTime.UtcNow;
        var since = DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc);
        // Batasi jendela agar query tetap ringan
        if (since < now.AddDays(-7)) since = now.AddDays(-7);
        if (since > now) since = now;

        var activity = await orderRepository.GetActivitySinceAsync(since);
        return new BoardActivityDto(activity.NewOrders, activity.NewlyPaid, activity.LatestAt, now);
    }

    // ==========================================
    // Penyusunan daftar
    // ==========================================

    private sealed record DayList(
        BoardFilter Filter, AdminFacetsDto Facets, List<DayContext> Filtered, Dictionary<string, int> TabCounts, List<DayContext> Listed);

    private sealed record OrderList(
        BoardFilter Filter, AdminFacetsDto Facets, List<OrderAggregate> Filtered, Dictionary<string, int> TabCounts, List<OrderAggregate> Listed);

    private async Task<DayList> BuildDayListAsync(AdminOrderBoardRequest request)
    {
        var filter = await ParseAsync(request, AdminBoardTabs.DayTabs);
        var rows = await LoadAsync(filter, wholeOrders: filter.ByCreatedDate);

        var facets = BuildFacets(rows.Select(r => new FacetSource(
            r.Row.FulfillmentType, ZoneKey(r.Row), r.Row.ZoneName, DriverKey(r.Row), r.Row.DriverName,
            r.Row.OutletId?.ToString(), r.Outlet?.Name, r.CategoryCodes, r.CategoryNames,
            r.PaymentStatus, VoucherKey(r.Row.VoucherCode), r.RefundStatus)).ToList(), includeDrivers: true);

        var filtered = rows.Where(r => MatchesFacets(filter, r.Row.FulfillmentType, ZoneKey(r.Row), DriverKey(r.Row),
            r.Row.OutletId?.ToString(), r.CategoryCodes, r.MenuIds, r.PaymentStatus, VoucherKey(r.Row.VoucherCode), r.RefundStatus,
            useDriver: true)).ToList();

        var tabCounts = AdminBoardTabs.DayTabs.ToDictionary(
            t => t,
            t => t == AdminBoardTabs.All ? filtered.Count : filtered.Count(r => InDayTab(r, t)));

        var listed = filtered.Where(r => filter.Tab == AdminBoardTabs.All || InDayTab(r, filter.Tab)).ToList();
        listed = filter.Sort switch
        {
            "DATE_DESC" => listed.OrderByDescending(r => r.Row.DeliveryDate).ThenBy(r => r.Row.OrderNumber).ToList(),
            "NEWEST" => listed.OrderByDescending(r => r.Row.OrderCreatedAt).ThenBy(r => r.Row.DeliveryDate).ToList(),
            "CUSTOMER" => listed.OrderBy(r => r.Row.CustomerName).ThenBy(r => r.Row.DeliveryDate).ToList(),
            // Default: tanggal kirim terdekat, yang perlu tindakan didahulukan dalam satu tanggal
            _ => listed.OrderBy(r => r.Row.DeliveryDate).ThenByDescending(r => r.NeedsAttention).ThenBy(r => r.Row.OrderNumber).ToList()
        };

        return new DayList(filter, facets, filtered, tabCounts, listed);
    }

    private async Task<OrderList> BuildOrderListAsync(AdminOrderBoardRequest request)
    {
        var filter = await ParseAsync(request, AdminBoardTabs.OrderTabs);
        // Semua hari dari pesanan yang cocok, agar progres & periode pesanan utuh walau hanya sebagian hari di periode
        var rows = await LoadAsync(filter, wholeOrders: true);

        var orders = rows.GroupBy(r => r.Row.OrderId).Select(ToOrderAggregate).ToList();

        var facets = BuildFacets(orders.Select(o => new FacetSource(
            o.Dto.FulfillmentType, o.ZoneKey, o.Dto.ZoneName, None, null,
            o.First.OutletId?.ToString(), o.Dto.OutletName, o.CategoryCodes, o.CategoryNames,
            o.Dto.PaymentStatus, VoucherKey(o.Dto.VoucherCode), o.RefundStatus)).ToList(), includeDrivers: false);

        var filtered = orders.Where(o => MatchesFacets(filter, o.Dto.FulfillmentType, o.ZoneKey, None,
            o.First.OutletId?.ToString(), o.CategoryCodes, o.MenuIds, o.Dto.PaymentStatus, VoucherKey(o.Dto.VoucherCode), o.RefundStatus,
            useDriver: false)).ToList();

        var tabCounts = AdminBoardTabs.OrderTabs.ToDictionary(
            t => t,
            t => t == AdminBoardTabs.All ? filtered.Count : filtered.Count(o => InOrderTab(o, t)));

        var listed = filtered.Where(o => filter.Tab == AdminBoardTabs.All || InOrderTab(o, filter.Tab)).ToList();
        listed = filter.Sort switch
        {
            "OLDEST" => listed.OrderBy(o => o.Dto.CreatedAt).ToList(),
            "DATE_ASC" => listed.OrderBy(o => o.Dto.FirstDeliveryDate).ThenBy(o => o.Dto.OrderNumber).ToList(),
            "TOTAL_DESC" => listed.OrderByDescending(o => o.Dto.GrandTotal).ThenByDescending(o => o.Dto.CreatedAt).ToList(),
            _ => listed.OrderByDescending(o => o.Dto.CreatedAt).ToList()
        };

        return new OrderList(filter, facets, filtered, tabCounts, listed);
    }

    // ==========================================
    // Helpers
    // ==========================================

    private sealed record BoardFilter(
        bool ByCreatedDate,
        DateOnly From,
        DateOnly To,
        TimeZoneInfo TimeZone,
        string? SearchPattern,
        HashSet<string> Fulfillment,
        HashSet<string> Zones,
        HashSet<string> Drivers,
        HashSet<string> Outlets,
        HashSet<string> Categories,
        HashSet<string> Payments,
        HashSet<string> Vouchers,
        HashSet<string> Refunds,
        Guid? MenuId,
        string Tab,
        string? Sort,
        int Page,
        int PageSize);

    /// <summary>Satu hari kirim beserta data turunan (menu, porsi, kategori, tindakan).</summary>
    private sealed class DayContext(AdminDayRow row, List<OrderItem> items, AddressSnapshotDto? address, OutletSnapshotDto? outlet)
    {
        public AdminDayRow Row { get; } = row;
        public List<OrderItem> Items { get; } = items;
        public AddressSnapshotDto? Address { get; } = address;
        public OutletSnapshotDto? Outlet { get; } = outlet;
        public int Portions { get; } = items.Sum(i => i.Qty);
        public HashSet<string> CategoryCodes { get; } = items.Select(i => i.CategoryCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> CategoryNames { get; } = items.GroupBy(i => i.CategoryCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CategoryName, StringComparer.OrdinalIgnoreCase);
        public HashSet<Guid> MenuIds { get; } = items.Select(i => i.MenuId).ToHashSet();
        public string PaymentStatus { get; } = PaymentStatusOf(row);

        public bool RefundPending => Row.RequiresManualRefund && Row.RefundedAt == null;

        public string? RefundStatus =>
            !Row.RequiresManualRefund ? null : RefundPending ? AdminRefundStatuses.Pending : AdminRefundStatuses.Done;

        public bool NeedsDriver =>
            Row.FulfillmentType == FulfillmentTypes.Delivery && NeedsDriverDayStatuses.Contains(Row.DayStatus) && Row.DriverUserId == null;

        public bool NeedsAttention => NeedsDriver || RefundPending || Row.PaymentNeedsReview;
    }

    private sealed record OrderAggregate(
        AdminOrderRowDto Dto,
        AdminDayRow First,
        string ZoneKey,
        string? RefundStatus,
        HashSet<string> CategoryCodes,
        Dictionary<string, string> CategoryNames,
        HashSet<Guid> MenuIds);

    private sealed record FacetSource(
        string FulfillmentType,
        string ZoneKey,
        string? ZoneName,
        string DriverKey,
        string? DriverName,
        string? OutletKey,
        string? OutletName,
        HashSet<string> CategoryCodes,
        Dictionary<string, string> CategoryNames,
        string PaymentStatus,
        string VoucherKey,
        string? RefundStatus);

    private async Task<BoardFilter> ParseAsync(AdminOrderBoardRequest request, IReadOnlyList<string> tabs)
    {
        var settings = await settingsService.GetEffectiveOrderSettingsAsync();
        var today = settings.GetLocalToday(DateTime.UtcNow);
        var from = request.DateFrom ?? today;
        var to = request.DateTo ?? from;
        if (to < from)
        {
            throw new InvalidOrderException("Tanggal akhir tidak boleh sebelum tanggal awal.");
        }
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            throw new InvalidOrderException($"Rentang tanggal maksimal {MaxRangeDays} hari.");
        }

        var basis = request.DateBasis?.Trim().ToUpperInvariant();
        if (basis is not (null or "" or "DELIVERY" or "CREATED"))
        {
            throw new InvalidOrderException("Acuan tanggal harus DELIVERY (tanggal kirim) atau CREATED (tanggal pesan).");
        }

        var tab = string.IsNullOrWhiteSpace(request.Tab) ? AdminBoardTabs.All : request.Tab.Trim().ToUpperInvariant();
        if (!tabs.Contains(tab))
        {
            throw new InvalidOrderException($"Tab '{request.Tab}' tidak dikenal.");
        }

        var q = request.Q?.Trim();
        string? pattern = null;
        if (!string.IsNullOrEmpty(q))
        {
            if (q.Length > 100)
            {
                throw new InvalidOrderException("Kata kunci pencarian maksimal 100 karakter.");
            }
            pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        }

        var fulfillment = SplitSet(request.Fulfillment, upper: true);
        if (fulfillment.Any(f => !FulfillmentTypes.All.Contains(f)))
        {
            throw new InvalidOrderException("Metode pemesanan harus DELIVERY atau PICKUP.");
        }

        var payments = SplitSet(request.Payments, upper: true);
        if (payments.Any(p => AdminPaymentStatuses.Options.All(o => o.Code != p)))
        {
            throw new InvalidOrderException("Status bayar tidak dikenal.");
        }

        var refunds = SplitSet(request.Refunds, upper: true);
        if (refunds.Any(r => AdminRefundStatuses.Options.All(o => o.Code != r)))
        {
            throw new InvalidOrderException("Status refund tidak dikenal.");
        }

        return new BoardFilter(
            ByCreatedDate: basis == "CREATED",
            From: from,
            To: to,
            TimeZone: settings.TimeZone,
            SearchPattern: pattern,
            Fulfillment: fulfillment,
            Zones: SplitSet(request.ZoneIds, upper: false),
            Drivers: SplitSet(request.DriverIds, upper: false),
            Outlets: SplitSet(request.OutletIds, upper: false),
            Categories: SplitSet(request.CategoryCodes, upper: true),
            Payments: payments,
            Vouchers: SplitSet(request.Vouchers, upper: true),
            Refunds: refunds,
            MenuId: request.MenuId,
            Tab: tab,
            Sort: request.Sort?.Trim().ToUpperInvariant(),
            Page: Math.Max(1, request.Page),
            PageSize: AllowedPageSizes.Contains(request.PageSize) ? request.PageSize : 20);
    }

    private static HashSet<string> SplitSet(string? value, bool upper) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => upper || v.Equals(None, StringComparison.OrdinalIgnoreCase) ? v.ToUpperInvariant() : v.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async Task<List<DayContext>> LoadAsync(BoardFilter filter, bool wholeOrders)
    {
        var query = new AdminDayRowQuery { SearchPattern = filter.SearchPattern, WholeOrders = wholeOrders };
        if (filter.ByCreatedDate)
        {
            query.CreatedFromUtc = TimeZoneInfo.ConvertTimeToUtc(filter.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), filter.TimeZone);
            query.CreatedToUtc = TimeZoneInfo.ConvertTimeToUtc(filter.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), filter.TimeZone);
            query.WholeOrders = true;
        }
        else
        {
            query.DeliveryFrom = OrderMapper.ToDbDate(filter.From);
            query.DeliveryTo = OrderMapper.ToDbDate(filter.To);
        }

        var rows = (await orderRepository.GetAdminDayRowsAsync(query)).ToList();
        var items = (await orderRepository.GetItemsByDayIdsAsync(rows.Select(r => r.OrderDayId).ToList()))
            .GroupBy(i => i.OrderDayId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return rows.Select(r => new DayContext(
            r,
            items.TryGetValue(r.OrderDayId, out var list) ? list : [],
            OrderMapper.DeserializeAddress(r.AddressSnapshot),
            OrderMapper.DeserializeOutlet(r.OutletSnapshot))).ToList();
    }

    private static string ZoneKey(AdminDayRow r) => r.ZoneId?.ToString() ?? None;
    private static string DriverKey(AdminDayRow r) => r.DriverUserId?.ToString() ?? None;
    private static string VoucherKey(string? code) => string.IsNullOrWhiteSpace(code) ? None : code.Trim().ToUpperInvariant();

    private static bool IsCancelled(AdminDayRow r) =>
        CancelledDayStatuses.Contains(r.DayStatus) || CancelledOrderStatuses.Contains(r.OrderStatus);

    /// <summary>Satu status bayar per pesanan. "Perlu ditinjau" didahulukan karena butuh tindakan admin.</summary>
    private static string PaymentStatusOf(AdminDayRow r) =>
        r.PaymentNeedsReview ? AdminPaymentStatuses.Review
        : r.PaidAt != null ? AdminPaymentStatuses.Paid
        : r.OrderStatus switch
        {
            OrderStatusCodes.AwaitingPayment => AdminPaymentStatuses.Awaiting,
            OrderStatusCodes.Expired => AdminPaymentStatuses.Expired,
            OrderStatusCodes.PaymentFailed => AdminPaymentStatuses.Failed,
            _ => AdminPaymentStatuses.Cancelled
        };

    private static List<ProductionMenuSummaryDto> Production(IEnumerable<DayContext> days) =>
        days.SelectMany(r => r.Items)
            .GroupBy(i => i.MenuId)
            .Select(g => new ProductionMenuSummaryDto(g.Key, g.First().MenuName, g.First().CategoryName, g.Sum(i => i.Qty)))
            .OrderByDescending(m => m.TotalQty).ThenBy(m => m.MenuName)
            .ToList();

    private static bool InDayTab(DayContext r, string tab)
    {
        if (tab == AdminBoardTabs.Attention)
        {
            return r.NeedsAttention;
        }

        var row = r.Row;
        var group =
            IsCancelled(row) ? AdminBoardTabs.Cancelled
            : row.OrderStatus == OrderStatusCodes.AwaitingPayment ? AdminBoardTabs.AwaitingPayment
            : DoneDayStatuses.Contains(row.DayStatus) ? AdminBoardTabs.Done
            : row.DayStatus;
        return group == tab;
    }

    private static bool InOrderTab(OrderAggregate o, string tab) => tab switch
    {
        AdminBoardTabs.Attention => o.Dto.NeedsAttention,
        AdminBoardTabs.Cancelled => CancelledOrderStatuses.Contains(o.Dto.OrderStatus),
        _ => o.Dto.OrderStatus == tab
    };

    /// <summary>
    /// Filter metode bertingkat. Pesan Antar lolos bila: tidak ada filter metode sama sekali, atau Pesan Antar /
    /// zona / driver dipilih, lalu cocok dengan zona & driver yang dipilih (bila ada). Ambil Sendiri sejenis untuk outlet.
    /// Kategori, menu, status bayar, voucher, dan refund berlaku untuk keduanya.
    /// </summary>
    private static bool MatchesFacets(
        BoardFilter f, string fulfillment, string zoneKey, string driverKey, string? outletKey,
        HashSet<string> categories, HashSet<Guid> menuIds, string paymentStatus, string voucherKey, string? refundStatus,
        bool useDriver)
    {
        var drivers = useDriver ? f.Drivers : [];
        var anyMethodFilter = f.Fulfillment.Count > 0 || f.Zones.Count > 0 || drivers.Count > 0 || f.Outlets.Count > 0;

        bool methodOk;
        if (!anyMethodFilter)
        {
            methodOk = true;
        }
        else if (fulfillment == FulfillmentTypes.Delivery)
        {
            methodOk = (f.Fulfillment.Contains(FulfillmentTypes.Delivery) || f.Zones.Count > 0 || drivers.Count > 0)
                && (f.Zones.Count == 0 || f.Zones.Contains(zoneKey))
                && (drivers.Count == 0 || drivers.Contains(driverKey));
        }
        else
        {
            methodOk = (f.Fulfillment.Contains(FulfillmentTypes.Pickup) || f.Outlets.Count > 0)
                && (f.Outlets.Count == 0 || (outletKey != null && f.Outlets.Contains(outletKey)));
        }

        return methodOk
            && (f.Categories.Count == 0 || categories.Overlaps(f.Categories))
            && (f.MenuId == null || menuIds.Contains(f.MenuId.Value))
            && (f.Payments.Count == 0 || f.Payments.Contains(paymentStatus))
            && (f.Vouchers.Count == 0 || f.Vouchers.Contains(voucherKey))
            && (f.Refunds.Count == 0 || (refundStatus != null && f.Refunds.Contains(refundStatus)));
    }

    private static AdminFacetsDto BuildFacets(List<FacetSource> sources, bool includeDrivers)
    {
        var delivery = sources.Where(s => s.FulfillmentType == FulfillmentTypes.Delivery).ToList();
        var pickup = sources.Where(s => s.FulfillmentType == FulfillmentTypes.Pickup).ToList();

        static List<AdminFacetOptionDto> Group(IEnumerable<(string Key, string? Label)> keys, string noneLabel) =>
            keys.GroupBy(k => k.Key)
                .Select(g => new AdminFacetOptionDto(g.Key, g.Key == None ? noneLabel : g.First().Label ?? g.Key, g.Count()))
                // Opsi "belum ada" selalu paling bawah
                .OrderBy(o => o.Id == None).ThenBy(o => o.Label)
                .ToList();

        // Opsi tetap (urutan sesuai daftar), hanya yang muncul di periode ini
        static List<AdminFacetOptionDto> Fixed(IEnumerable<string?> keys, IReadOnlyList<(string Code, string Label)> options)
        {
            var counts = keys.Where(k => k != null).GroupBy(k => k!).ToDictionary(g => g.Key, g => g.Count());
            return options.Where(o => counts.ContainsKey(o.Code))
                .Select(o => new AdminFacetOptionDto(o.Code, o.Label, counts[o.Code]))
                .ToList();
        }

        return new AdminFacetsDto(
            DeliveryCount: delivery.Count,
            PickupCount: pickup.Count,
            Zones: Group(delivery.Select(s => (s.ZoneKey, s.ZoneName)), "Belum ada zona"),
            Drivers: includeDrivers ? Group(delivery.Select(s => (s.DriverKey, s.DriverName)), "Belum ada driver") : [],
            Outlets: Group(pickup.Select(s => (s.OutletKey ?? None, s.OutletName)), "Outlet tidak tercatat"),
            Categories: sources
                .SelectMany(s => s.CategoryCodes.Select(c => (Code: c.ToUpperInvariant(), Name: s.CategoryNames.GetValueOrDefault(c, c))))
                .GroupBy(c => c.Code)
                .Select(g => new AdminFacetOptionDto(g.Key, g.First().Name, g.Count()))
                .OrderBy(o => o.Label)
                .ToList(),
            Payments: Fixed(sources.Select(s => s.PaymentStatus), AdminPaymentStatuses.Options),
            Vouchers: Group(sources.Select(s => (s.VoucherKey, (string?)s.VoucherKey)), "Tanpa voucher"),
            Refunds: Fixed(sources.Select(s => s.RefundStatus), AdminRefundStatuses.Options));
    }

    private static AdminDayRowDto ToDayDto(DayContext r)
    {
        var row = r.Row;
        var firstMenu = r.Items.OrderByDescending(i => i.Qty).ThenBy(i => i.MenuName).FirstOrDefault();
        return new AdminDayRowDto(
            OrderDayId: row.OrderDayId,
            OrderId: row.OrderId,
            OrderNumber: row.OrderNumber,
            CustomerName: row.CustomerName,
            CustomerPhone: row.CustomerPhone,
            DeliveryDate: OrderMapper.ToDateOnly(row.DeliveryDate),
            DayIndex: row.DayIndex,
            DayCount: row.DayCount,
            DayStatus: row.DayStatus,
            OrderStatus: row.OrderStatus,
            FulfillmentType: row.FulfillmentType,
            DayUpdatedAt: row.DayUpdatedAt,
            OrderCreatedAt: row.OrderCreatedAt,
            FirstMenuName: firstMenu?.MenuName,
            MenuCount: r.Items.Select(i => i.MenuId).Distinct().Count(),
            Portions: r.Portions,
            Notes: row.Notes,
            RecipientName: r.Address?.RecipientName,
            RecipientPhone: r.Address?.PhoneNumber,
            AddressLine: r.Address == null ? null : $"{r.Address.FullAddress}, {r.Address.Subdistrict}",
            ZoneId: row.ZoneId,
            ZoneName: row.ZoneName,
            DriverUserId: row.DriverUserId,
            DriverName: row.DriverName,
            AssignmentSource: row.AssignmentSource,
            OutletId: row.OutletId,
            OutletName: r.Outlet?.Name,
            PickupHours: r.Outlet?.PickupHours,
            PaymentDueAt: row.PaymentDueAt,
            PaidAt: row.PaidAt,
            PaymentNeedsReview: row.PaymentNeedsReview,
            PaymentStatus: r.PaymentStatus,
            VoucherCode: row.VoucherCode,
            RequiresManualRefund: row.RequiresManualRefund,
            RefundPending: r.RefundPending,
            DaySubtotal: row.DaySubtotal,
            RefundedAt: row.RefundedAt,
            NeedsDriver: r.NeedsDriver,
            NeedsAttention: r.NeedsAttention);
    }

    private static OrderAggregate ToOrderAggregate(IGrouping<Guid, DayContext> group)
    {
        var days = group.OrderBy(d => d.Row.DeliveryDate).ToList();
        var first = days[0].Row;
        var zoneDay = days.FirstOrDefault(d => d.Row.ZoneId != null)?.Row;
        var refundPending = days.Where(d => d.RefundPending).ToList();
        var refundedDays = days.Count(d => d.Row.RequiresManualRefund && d.Row.RefundedAt != null);
        var needsDriverDays = days.Count(d => d.NeedsDriver);
        var needsAttention = first.PaymentNeedsReview || refundPending.Count > 0 || needsDriverDays > 0;

        var dto = new AdminOrderRowDto(
            OrderId: first.OrderId,
            OrderNumber: first.OrderNumber,
            CustomerName: first.CustomerName,
            CustomerPhone: first.CustomerPhone,
            CreatedAt: first.OrderCreatedAt,
            OrderStatus: first.OrderStatus,
            FulfillmentType: first.FulfillmentType,
            ZoneName: zoneDay?.ZoneName,
            OutletName: days[0].Outlet?.Name,
            FirstDeliveryDate: OrderMapper.ToDateOnly(days[0].Row.DeliveryDate),
            LastDeliveryDate: OrderMapper.ToDateOnly(days[^1].Row.DeliveryDate),
            DayCount: days.Count,
            DoneDays: days.Count(d => DoneDayStatuses.Contains(d.Row.DayStatus)),
            CancelledDays: days.Count(d => CancelledDayStatuses.Contains(d.Row.DayStatus)),
            Portions: days.Where(d => !CancelledDayStatuses.Contains(d.Row.DayStatus)).Sum(d => d.Portions),
            GrandTotal: first.GrandTotal,
            PaymentDueAt: first.PaymentDueAt,
            PaidAt: first.PaidAt,
            PaymentNeedsReview: first.PaymentNeedsReview,
            PaymentStatus: days[0].PaymentStatus,
            VoucherCode: first.VoucherCode,
            RefundPendingDays: refundPending.Count,
            RefundPendingAmount: refundPending.Sum(d => d.Row.DaySubtotal),
            RefundedDays: refundedDays,
            NeedsDriverDays: needsDriverDays,
            NeedsAttention: needsAttention);

        var categoryNames = days.SelectMany(d => d.CategoryNames)
            .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

        var refundStatus = refundPending.Count > 0 ? AdminRefundStatuses.Pending
            : refundedDays > 0 ? AdminRefundStatuses.Done
            : null;

        return new OrderAggregate(
            dto,
            first,
            zoneDay?.ZoneId?.ToString() ?? None,
            refundStatus,
            days.SelectMany(d => d.CategoryCodes).ToHashSet(StringComparer.OrdinalIgnoreCase),
            categoryNames,
            days.SelectMany(d => d.MenuIds).ToHashSet());
    }

    // ==========================================
    // Label ekspor
    // ==========================================

    private static DateTime ToLocal(DateTime utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    private static string FileName(string prefix, BoardFilter filter)
    {
        var basis = filter.ByCreatedDate ? "pesan" : "kirim";
        var range = filter.From == filter.To ? $"{filter.From:yyyy-MM-dd}" : $"{filter.From:yyyy-MM-dd}_{filter.To:yyyy-MM-dd}";
        return $"{prefix}_{basis}_{range}.xlsx";
    }

    private static string FulfillmentLabel(string code) =>
        code == FulfillmentTypes.Delivery ? "Pesan Antar" : "Ambil Sendiri";

    private static string PaymentLabel(string code) =>
        AdminPaymentStatuses.Options.FirstOrDefault(o => o.Code == code).Label ?? code;

    private static string? RefundLabel(AdminDayRow row, TimeZoneInfo tz) =>
        !row.RequiresManualRefund ? null
        : row.RefundedAt == null ? "Tertunda"
        : $"Sudah {ToLocal(row.RefundedAt.Value, tz):dd/MM/yyyy}" + (row.RefundNote == null ? string.Empty : $" · {row.RefundNote}");

    private static string DayStatusLabel(AdminDayRow row) =>
        row.OrderStatus == OrderStatusCodes.AwaitingPayment && row.DayStatus == OrderDayStatusCodes.Scheduled
            ? "Menunggu bayar"
            : row.DayStatus switch
            {
                OrderDayStatusCodes.Scheduled => "Terjadwal",
                OrderDayStatusCodes.Confirmed => "Dikonfirmasi",
                OrderDayStatusCodes.Preparing => "Disiapkan",
                OrderDayStatusCodes.Ready => "Siap",
                OrderDayStatusCodes.OutForDelivery => "Dibawa kurir",
                OrderDayStatusCodes.Delivered => "Diterima",
                OrderDayStatusCodes.PickedUp => "Diambil",
                OrderDayStatusCodes.Cancelled => "Dibatalkan",
                OrderDayStatusCodes.CancelledByAdmin => "Dibatalkan admin",
                _ => row.DayStatus
            };

    private static string OrderStatusLabel(string code) => code switch
    {
        OrderStatusCodes.AwaitingPayment => "Menunggu bayar",
        OrderStatusCodes.Paid => "Dibayar",
        OrderStatusCodes.InProgress => "Diproses",
        OrderStatusCodes.Completed => "Selesai",
        OrderStatusCodes.CancelledByCustomer => "Dibatalkan pelanggan",
        OrderStatusCodes.CancelledByAdmin => "Dibatalkan admin",
        OrderStatusCodes.Expired => "Kedaluwarsa",
        OrderStatusCodes.PaymentFailed => "Gagal bayar",
        _ => code
    };
}

/// <summary>Berkas hasil ekspor.</summary>
public sealed record AdminExportFile(byte[] Content, string FileName);
