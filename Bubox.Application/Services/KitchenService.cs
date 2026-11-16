using Bubox.Application.DTOs.Kitchen;
using Bubox.Application.DTOs.Order;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

/// <summary>
/// Dapur: admin memvalidasi pesanan satu tanggal kirim (H-1, setelah cutoff + batas waktu bayar) lalu merilisnya.
/// Dapur hanya melihat tanggal yang sudah dirilis, dikelompokkan per outlet (Ambil Sendiri) dan per zona (Pesan Antar).
/// </summary>
public class KitchenService(
    IOrderRepository orderRepository,
    IKitchenRepository kitchenRepository,
    ISettingsService settingsService,
    IOrderAdminService orderAdminService) : IKitchenService
{
    private const string None = "NONE";
    private const int MaxBulkItems = 200;

    private static readonly HashSet<string> ReadyStatuses =
    [
        OrderDayStatusCodes.Ready, OrderDayStatusCodes.OutForDelivery, OrderDayStatusCodes.Delivered, OrderDayStatusCodes.PickedUp
    ];

    private static readonly HashSet<string> NeedsDriverStatuses =
    [
        OrderDayStatusCodes.Confirmed, OrderDayStatusCodes.Preparing, OrderDayStatusCodes.Ready
    ];

    // ==========================================
    // Validasi & rilis (admin)
    // ==========================================

    public async Task<KitchenValidationDto> GetValidationAsync(DateOnly date)
    {
        var settings = await settingsService.GetEffectiveOrderSettingsAsync();
        var nowUtc = DateTime.UtcNow;
        var days = await LoadDaysAsync(date);
        var release = await kitchenRepository.GetReleaseAsync(OrderMapper.ToDbDate(date));

        var cutoffAt = settings.GetCutoffUtc(date);
        var cutoffPassed = nowUtc >= cutoffAt;

        var awaiting = days.Where(d => d.Row.OrderStatus == OrderStatusCodes.AwaitingPayment)
            .GroupBy(d => d.Row.OrderId)
            .Select(g => g.First())
            .Select(d => new KitchenIssueDto(d.Row.OrderId, null, d.Row.OrderNumber, d.Row.CustomerName,
                $"Batas bayar {ToLocal(d.Row.PaymentDueAt, settings.TimeZone):HH.mm}"))
            .ToList();

        var production = days.Where(d => OrderDayStatusCodes.Production.Contains(d.Row.DayStatus)).ToList();

        var needsDriver = production
            .Where(d => d.Row.FulfillmentType == FulfillmentTypes.Delivery && NeedsDriverStatuses.Contains(d.Row.DayStatus) && d.Row.DriverUserId == null)
            .Select(d => new KitchenIssueDto(d.Row.OrderId, d.Row.OrderDayId, d.Row.OrderNumber, d.RecipientName,
                d.Row.ZoneName ?? "Belum ada zona"))
            .ToList();

        var review = production.Where(d => d.Row.PaymentNeedsReview)
            .GroupBy(d => d.Row.OrderId)
            .Select(g => g.First())
            .Select(d => new KitchenIssueDto(d.Row.OrderId, null, d.Row.OrderNumber, d.Row.CustomerName, d.Row.PaymentReviewNote))
            .ToList();

        var blockers = new List<string>();
        if (!cutoffPassed)
        {
            blockers.Add($"Cutoff belum lewat (pesanan masih bisa masuk sampai {ToLocal(cutoffAt, settings.TimeZone):dd/MM HH.mm}).");
        }
        if (awaiting.Count > 0)
        {
            blockers.Add($"{awaiting.Count} pesanan masih menunggu pembayaran.");
        }

        var groups = BuildGroups(production)
            .Select(g => new KitchenGroupSummaryDto(g.Key, g.Kind, g.Name, g.Subtitle, g.Days.Count, g.Days.Sum(d => d.Portions)))
            .ToList();

        return new KitchenValidationDto(
            Date: date,
            Release: ToReleaseDto(release),
            CutoffAt: cutoffAt,
            CutoffPassed: cutoffPassed,
            PaymentClosesAt: cutoffAt.AddMinutes(settings.PaymentTimeoutMinutes),
            AwaitingPayment: awaiting,
            NeedsDriver: needsDriver,
            PaymentReview: review,
            CanRelease: blockers.Count == 0,
            Blockers: blockers,
            Packages: production.Count,
            Portions: production.Sum(d => d.Portions),
            Recap: Recap(production),
            Groups: groups);
    }

    public async Task<KitchenValidationDto> ReleaseAsync(DateOnly date, Guid adminId)
    {
        var validation = await GetValidationAsync(date);
        if (!validation.CanRelease)
        {
            throw new InvalidOrderException("Belum bisa dirilis: " + string.Join(" ", validation.Blockers));
        }

        await kitchenRepository.UpsertReleaseAsync(new KitchenRelease
        {
            DeliveryDate = OrderMapper.ToDbDate(date),
            ReleasedAt = DateTime.UtcNow,
            ReleasedBy = adminId,
            DayCount = validation.Packages,
            Portions = validation.Portions
        });

        return await GetValidationAsync(date);
    }

    public async Task CancelReleaseAsync(DateOnly date)
    {
        if (!await kitchenRepository.DeleteReleaseAsync(OrderMapper.ToDbDate(date)))
        {
            throw new InvalidOrderException("Tanggal ini belum dirilis ke dapur.");
        }
    }

    // ==========================================
    // Papan dapur (hari H)
    // ==========================================

    public async Task<KitchenBoardDto> GetBoardAsync(DateOnly date)
    {
        var release = await kitchenRepository.GetReleaseAsync(OrderMapper.ToDbDate(date));
        if (release == null)
        {
            return new KitchenBoardDto(date, false, null, 0, 0, 0, [], []);
        }

        var days = await LoadDaysAsync(date);
        // Yang dimasak + yang dibatalkan admin setelah rilis (ditandai agar tidak ikut dikemas)
        var shown = days.Where(d =>
                OrderDayStatusCodes.Production.Contains(d.Row.DayStatus)
                || (d.Row.DayStatus == OrderDayStatusCodes.CancelledByAdmin && d.Row.DayCancelledAt > release.ReleasedAt))
            .ToList();
        var active = shown.Where(d => !IsCancelled(d)).ToList();

        var groups = BuildGroups(shown).Select(g =>
        {
            var groupActive = g.Days.Where(d => !IsCancelled(d)).ToList();
            return new KitchenGroupDto(
                Key: g.Key,
                Kind: g.Kind,
                Name: g.Name,
                Subtitle: g.Subtitle,
                Packages: groupActive.Count,
                ReadyPackages: groupActive.Count(d => ReadyStatuses.Contains(d.Row.DayStatus)),
                Portions: groupActive.Sum(d => d.Portions),
                Recap: Recap(groupActive),
                Items: g.Days
                    .OrderBy(IsCancelled)
                    .ThenBy(d => d.RecipientName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(d => d.Row.OrderNumber)
                    .Select(d => new KitchenPackageDto(
                        OrderDayId: d.Row.OrderDayId,
                        OrderNumber: d.Row.OrderNumber,
                        RecipientName: d.RecipientName,
                        Items: d.Items.OrderBy(i => i.CategoryName).ThenBy(i => i.MenuName)
                            .Select(i => new KitchenItemDto(i.MenuName, i.CategoryName, i.Qty)).ToList(),
                        Notes: d.Row.Notes,
                        DayStatus: d.Row.DayStatus,
                        IsReady: ReadyStatuses.Contains(d.Row.DayStatus),
                        CancelledAfterRelease: IsCancelled(d)))
                    .ToList());
        }).ToList();

        return new KitchenBoardDto(
            Date: date,
            Released: true,
            Release: ToReleaseDto(release),
            Packages: active.Count,
            ReadyPackages: active.Count(d => ReadyStatuses.Contains(d.Row.DayStatus)),
            Portions: active.Sum(d => d.Portions),
            Recap: Recap(active),
            Groups: groups);
    }

    public async Task<BulkActionResultDto> MarkReadyAsync(DateOnly date, IReadOnlyCollection<Guid> orderDayIds, OrderActor actor)
    {
        var ids = (orderDayIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > MaxBulkItems)
        {
            throw new InvalidOrderException($"Pilih 1-{MaxBulkItems} paket.");
        }

        if (await kitchenRepository.GetReleaseAsync(OrderMapper.ToDbDate(date)) == null)
        {
            throw new InvalidOrderException("Tanggal ini belum dirilis ke dapur.");
        }

        var rows = (await orderRepository.GetAdminDayRowsAsync(new AdminDayRowQuery
        {
            DeliveryFrom = OrderMapper.ToDbDate(date),
            DeliveryTo = OrderMapper.ToDbDate(date)
        })).ToDictionary(r => r.OrderDayId);

        var succeeded = 0;
        var failed = new List<BulkActionFailureDto>();
        foreach (var id in ids)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                failed.Add(new BulkActionFailureDto(id, null, "Paket ini bukan untuk tanggal kirim ini."));
                continue;
            }

            try
            {
                await orderAdminService.MarkDayReadyAsync(id, actor);
                succeeded++;
            }
            catch (Exception ex) when (ex is OrderNotFoundException or InvalidOrderException or InvalidStatusTransitionException
                                           or ForbiddenStatusTransitionException or OrderConcurrencyException)
            {
                failed.Add(new BulkActionFailureDto(id, row.OrderNumber, ex.Message));
            }
        }

        return new BulkActionResultDto(succeeded, failed);
    }

    // ==========================================
    // Helpers
    // ==========================================

    private sealed class KitchenDay(AdminDayRow row, List<OrderItem> items, AddressSnapshotDto? address, OutletSnapshotDto? outlet)
    {
        public AdminDayRow Row { get; } = row;
        public List<OrderItem> Items { get; } = items;
        public OutletSnapshotDto? Outlet { get; } = outlet;
        public int Portions { get; } = items.Sum(i => i.Qty);
        /// <summary>Penerima (Pesan Antar) atau nama pelanggan (Ambil Sendiri).</summary>
        public string RecipientName { get; } = address?.RecipientName ?? row.CustomerName;
    }

    private sealed record Group(string Key, string Kind, string Name, string? Subtitle, string SortKey, List<KitchenDay> Days);

    private async Task<List<KitchenDay>> LoadDaysAsync(DateOnly date)
    {
        var rows = (await orderRepository.GetAdminDayRowsAsync(new AdminDayRowQuery
        {
            DeliveryFrom = OrderMapper.ToDbDate(date),
            DeliveryTo = OrderMapper.ToDbDate(date)
        })).ToList();

        var items = (await orderRepository.GetItemsByDayIdsAsync(rows.Select(r => r.OrderDayId).ToList()))
            .GroupBy(i => i.OrderDayId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return rows.Select(r => new KitchenDay(
            r,
            items.TryGetValue(r.OrderDayId, out var list) ? list : [],
            OrderMapper.DeserializeAddress(r.AddressSnapshot),
            OrderMapper.DeserializeOutlet(r.OutletSnapshot))).ToList();
    }

    private static bool IsCancelled(KitchenDay d) => d.Row.DayStatus == OrderDayStatusCodes.CancelledByAdmin;

    /// <summary>
    /// Kelompok kemas: outlet (urut jam ambil), lalu zona (urut nama), lalu "Belum ada zona".
    /// </summary>
    private static List<Group> BuildGroups(IEnumerable<KitchenDay> days)
    {
        var groups = days
            .GroupBy(d => d.Row.FulfillmentType == FulfillmentTypes.Pickup
                ? $"{KitchenGroupKinds.Outlet}:{d.Row.OutletId?.ToString() ?? None}"
                : $"{KitchenGroupKinds.Zone}:{d.Row.ZoneId?.ToString() ?? None}")
            .Select(g =>
            {
                var first = g.First();
                if (first.Row.FulfillmentType == FulfillmentTypes.Pickup)
                {
                    var hours = first.Outlet?.PickupHours;
                    var name = first.Outlet?.Name ?? "Outlet tidak tercatat";
                    return new Group(g.Key, KitchenGroupKinds.Outlet, name, hours == null ? null : $"Ambil {hours}",
                        $"0|{hours ?? "~"}|{name}", g.ToList());
                }

                var noZone = first.Row.ZoneId == null;
                var drivers = g.Select(d => d.Row.DriverName).Where(n => !string.IsNullOrEmpty(n)).Distinct().OrderBy(n => n).ToList();
                var subtitle = drivers.Count > 0 ? $"Driver: {string.Join(", ", drivers)}" : "Belum ada driver";
                var zoneName = first.Row.ZoneName ?? "Belum ada zona";
                return new Group(g.Key, KitchenGroupKinds.Zone, zoneName, subtitle, $"{(noZone ? 2 : 1)}|{zoneName}", g.ToList());
            })
            .OrderBy(g => g.SortKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return groups;
    }

    private static List<KitchenRecapMenuDto> Recap(IEnumerable<KitchenDay> days) =>
        days.SelectMany(d => d.Items)
            .GroupBy(i => i.MenuId)
            .Select(g => new KitchenRecapMenuDto(g.Key, g.First().MenuName, g.First().CategoryName, g.Sum(i => i.Qty)))
            .OrderByDescending(m => m.Qty).ThenBy(m => m.MenuName)
            .ToList();

    private static KitchenReleaseInfoDto? ToReleaseDto(KitchenRelease? release) =>
        release == null ? null : new KitchenReleaseInfoDto(release.ReleasedAt, release.ReleasedByName, release.DayCount, release.Portions);

    private static DateTime ToLocal(DateTime utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
}
