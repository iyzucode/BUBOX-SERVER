using Bubox.Application.DTOs.Order;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
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

/// <summary>
/// Layar admin Pesanan: daftar per hari kirim & per pesanan dengan filter bertingkat, tab status, jumlah per opsi
/// filter, ringkasan, dan rekap dapur. Data satu periode diambil sekali dari DB lalu difilter di memori (jumlah
/// pesanan katering per periode kecil), sehingga semua hitungan konsisten satu sama lain.
/// </summary>
public class AdminOrderBoardService(
    IOrderRepository orderRepository,
    ISettingsService settingsService,
    IOrderService orderService) : IAdminOrderBoardService
{
    private const string None = "NONE";
    private const int MaxRangeDays = 93;
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

    /// <summary>Hari yang masuk rekap dapur: sudah dibayar dan belum batal.</summary>
    private static readonly HashSet<string> ProductionDayStatuses =
    [
        OrderDayStatusCodes.Confirmed, OrderDayStatusCodes.Preparing, OrderDayStatusCodes.Ready,
        OrderDayStatusCodes.OutForDelivery, OrderDayStatusCodes.Delivered, OrderDayStatusCodes.PickedUp
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
        var filter = await ParseAsync(request, AdminBoardTabs.DayTabs);
        var rows = await LoadAsync(filter, wholeOrders: filter.ByCreatedDate);

        var facets = BuildFacets(
            rows.Select(r => new FacetSource(r.Row.FulfillmentType, ZoneKey(r.Row), r.Row.ZoneName, DriverKey(r.Row), r.Row.DriverName,
                r.Row.OutletId?.ToString(), r.Outlet?.Name, r.CategoryCodes, r.CategoryNames)).ToList(),
            includeDrivers: true);

        var filtered = rows.Where(r => MatchesFacets(filter, r.Row.FulfillmentType, ZoneKey(r.Row), DriverKey(r.Row),
            r.Row.OutletId?.ToString(), r.CategoryCodes, r.MenuIds, useDriver: true)).ToList();

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

        var summary = new AdminBoardSummaryDto(
            Rows: listed.Count,
            Orders: listed.Select(r => r.Row.OrderId).Distinct().Count(),
            Portions: listed.Where(r => !IsCancelled(r.Row)).Sum(r => r.Portions),
            PaidRevenue: listed.Where(r => PaidOrderStatuses.Contains(r.Row.OrderStatus) && !CancelledDayStatuses.Contains(r.Row.DayStatus))
                .Sum(r => r.Row.DaySubtotal),
            AwaitingPayment: listed.Count(r => InDayTab(r, AdminBoardTabs.AwaitingPayment)),
            NeedsAttention: listed.Count(r => r.NeedsAttention));

        // Rekap dapur: seluruh hari yang lolos filter (bukan hanya tab aktif), sudah dibayar & belum batal
        var production = filtered
            .Where(r => ProductionDayStatuses.Contains(r.Row.DayStatus))
            .SelectMany(r => r.Items)
            .GroupBy(i => i.MenuId)
            .Select(g => new ProductionMenuSummaryDto(g.Key, g.First().MenuName, g.First().CategoryName, g.Sum(i => i.Qty)))
            .OrderByDescending(m => m.TotalQty).ThenBy(m => m.MenuName)
            .ToList();

        var page = listed.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(ToDayDto).ToList();
        return new AdminBoardResult<AdminDayRowDto>(page, filter.Page, filter.PageSize, listed.Count, tabCounts, facets, summary, production);
    }

    // ==========================================
    // Per pesanan
    // ==========================================

    public async Task<AdminBoardResult<AdminOrderRowDto>> SearchOrdersAsync(AdminOrderBoardRequest request)
    {
        var filter = await ParseAsync(request, AdminBoardTabs.OrderTabs);
        // Semua hari dari pesanan yang cocok, agar progres & periode pesanan utuh walau hanya sebagian hari di periode
        var rows = await LoadAsync(filter, wholeOrders: true);

        var orders = rows.GroupBy(r => r.Row.OrderId).Select(ToOrderAggregate).ToList();

        var facets = BuildFacets(
            orders.Select(o => new FacetSource(o.FulfillmentType, o.ZoneKey, o.ZoneName, None, null,
                o.OutletId?.ToString(), o.OutletName, o.CategoryCodes, o.CategoryNames)).ToList(),
            includeDrivers: false);

        var filtered = orders.Where(o => MatchesFacets(filter, o.FulfillmentType, o.ZoneKey, None,
            o.OutletId?.ToString(), o.CategoryCodes, o.MenuIds, useDriver: false)).ToList();

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

        var summary = new AdminBoardSummaryDto(
            Rows: listed.Count,
            Orders: listed.Count,
            Portions: listed.Sum(o => o.Dto.Portions),
            PaidRevenue: listed.Where(o => PaidOrderStatuses.Contains(o.Dto.OrderStatus)).Sum(o => o.Dto.GrandTotal),
            AwaitingPayment: listed.Count(o => o.Dto.OrderStatus == OrderStatusCodes.AwaitingPayment),
            NeedsAttention: listed.Count(o => o.Dto.NeedsAttention));

        var page = listed.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(o => o.Dto).ToList();
        return new AdminBoardResult<AdminOrderRowDto>(page, filter.Page, filter.PageSize, listed.Count, tabCounts, facets, summary, []);
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
            Assignments: rows.Select(r => new AdminDayAssignmentDto(r.OrderDayId, r.ZoneName, r.DriverUserId, r.DriverName, r.AssignmentSource)).ToList());
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

        public bool NeedsDriver =>
            Row.FulfillmentType == FulfillmentTypes.Delivery && NeedsDriverDayStatuses.Contains(Row.DayStatus) && Row.DriverUserId == null;

        public bool NeedsAttention => NeedsDriver || Row.RequiresManualRefund || Row.PaymentNeedsReview;
    }

    private sealed record OrderAggregate(
        AdminOrderRowDto Dto,
        string FulfillmentType,
        string ZoneKey,
        string? ZoneName,
        Guid? OutletId,
        string? OutletName,
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
        Dictionary<string, string> CategoryNames);

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

    private static bool IsCancelled(AdminDayRow r) =>
        CancelledDayStatuses.Contains(r.DayStatus) || CancelledOrderStatuses.Contains(r.OrderStatus);

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
    /// Kategori & menu berlaku untuk keduanya.
    /// </summary>
    private static bool MatchesFacets(
        BoardFilter f, string fulfillment, string zoneKey, string driverKey, string? outletKey,
        HashSet<string> categories, HashSet<Guid> menuIds, bool useDriver)
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
            && (f.MenuId == null || menuIds.Contains(f.MenuId.Value));
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
                .ToList());
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
            RequiresManualRefund: row.RequiresManualRefund,
            NeedsDriver: r.NeedsDriver,
            NeedsAttention: r.NeedsAttention);
    }

    private static OrderAggregate ToOrderAggregate(IGrouping<Guid, DayContext> group)
    {
        var days = group.OrderBy(d => d.Row.DeliveryDate).ToList();
        var first = days[0].Row;
        var zoneDay = days.FirstOrDefault(d => d.Row.ZoneId != null)?.Row;
        var refundDays = days.Where(d => d.Row.RequiresManualRefund).ToList();
        var needsDriverDays = days.Count(d => d.NeedsDriver);
        var needsAttention = first.PaymentNeedsReview || refundDays.Count > 0 || needsDriverDays > 0;

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
            RefundPendingDays: refundDays.Count,
            RefundPendingAmount: refundDays.Sum(d => d.Row.DaySubtotal),
            NeedsDriverDays: needsDriverDays,
            NeedsAttention: needsAttention);

        var categoryNames = days.SelectMany(d => d.CategoryNames)
            .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

        return new OrderAggregate(
            dto,
            first.FulfillmentType,
            zoneDay?.ZoneId?.ToString() ?? None,
            zoneDay?.ZoneName,
            first.OutletId,
            days[0].Outlet?.Name,
            days.SelectMany(d => d.CategoryCodes).ToHashSet(StringComparer.OrdinalIgnoreCase),
            categoryNames,
            days.SelectMany(d => d.MenuIds).ToHashSet());
    }
}
