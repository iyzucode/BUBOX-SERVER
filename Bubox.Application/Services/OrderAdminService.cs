using Bubox.Application.DTOs.Order;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class OrderAdminService(
    IOrderRepository orderRepository,
    IOrderUnitOfWorkFactory unitOfWorkFactory,
    ISettingsService settingsService,
    IPaymentGateway paymentGateway,
    OrderStatusEngine statusEngine) : IOrderAdminService
{
    public async Task<PagedResult<OrderSummaryDto>> SearchOrdersAsync(OrderSearchRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        string? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            status = request.Status.Trim().ToUpperInvariant();
            if (!OrderStatusCodes.All.Contains(status))
            {
                throw new InvalidOrderException($"Status '{request.Status}' tidak dikenal.");
            }
        }

        // Filter tanggal dibuat memakai tanggal lokal (zona waktu setting), lalu dikonversi ke UTC
        var settings = await settingsService.GetEffectiveOrderSettingsAsync();
        DateTime? ToUtc(DateOnly? date) => date.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(OrderMapper.ToDbDate(date.Value), settings.TimeZone)
            : null;

        var rows = (await orderRepository.SearchAsync(new OrderSearchFilter
        {
            StatusCode = status,
            DeliveryDate = request.DeliveryDate.HasValue ? OrderMapper.ToDbDate(request.DeliveryDate.Value) : null,
            CreatedFromUtc = ToUtc(request.CreatedFrom),
            CreatedToUtc = ToUtc(request.CreatedTo?.AddDays(1)),
            Search = request.Search,
            Page = page,
            PageSize = pageSize
        })).ToList();

        return new PagedResult<OrderSummaryDto>(
            Items: rows.Select(OrderMapper.ToSummary).ToList(),
            Page: page,
            PageSize: pageSize,
            TotalCount: rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public async Task<DailyProductionDto> GetDailyProductionAsync(DateOnly date)
    {
        var orders = await LoadDailyAsync(date, OrderDayStatusCodes.Production, fulfillmentType: null);

        var menus = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.MenuId, i.MenuName, i.CategoryName })
            .Select(g => new ProductionMenuSummaryDto(g.Key.MenuId, g.Key.MenuName, g.Key.CategoryName, g.Sum(i => i.Qty)))
            .OrderBy(m => m.CategoryName)
            .ThenBy(m => m.MenuName)
            .ToList();

        return new DailyProductionDto(
            Date: date,
            TotalOrders: orders.Count,
            TotalPortions: menus.Sum(m => m.TotalQty),
            Menus: menus,
            Orders: orders);
    }

    public async Task<DailyDeliveriesDto> GetDailyDeliveriesAsync(DateOnly date)
    {
        var deliveries = await LoadDailyAsync(date, OrderDayStatusCodes.Production, FulfillmentTypes.Delivery);
        return new DailyDeliveriesDto(date, deliveries);
    }

    public async Task UpdateDayStatusAsync(Guid orderDayId, string statusCode, OrderActor actor)
    {
        var target = statusCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!OrderDayStatusCodes.ManualProgress.Contains(target))
        {
            throw new InvalidOrderException(
                $"Status '{statusCode}' tidak dapat di-set manual. Gunakan CancelDayByAdmin untuk pembatalan force majeure.");
        }

        var nowUtc = DateTime.UtcNow;
        var dayRef = await orderRepository.GetDayByIdAsync(orderDayId)
            ?? throw new OrderNotFoundException("Hari kirim tidak ditemukan.");

        await using var uow = await unitOfWorkFactory.BeginAsync();
        var order = await uow.LockOrderAsync(dayRef.OrderId)
            ?? throw new OrderNotFoundException();
        var days = await uow.GetDaysAsync(order.Id);
        var day = days.First(d => d.Id == orderDayId);

        if (target is OrderDayStatusCodes.OutForDelivery or OrderDayStatusCodes.Delivered
            && order.FulfillmentType != FulfillmentTypes.Delivery)
        {
            throw new InvalidStatusTransitionException("Pesanan ini diambil sendiri (PICKUP), bukan diantar.");
        }

        if (target == OrderDayStatusCodes.PickedUp && order.FulfillmentType != FulfillmentTypes.Pickup)
        {
            throw new InvalidStatusTransitionException("Pesanan ini diantar (DELIVERY), bukan diambil sendiri.");
        }

        await statusEngine.ChangeDayStatusAsync(uow, day, target, actor, null, nowUtc);

        // Berangkat: bekukan zona & driver saat ini supaya riwayat tidak ikut berubah bila pemetaan diganti
        if (target == OrderDayStatusCodes.OutForDelivery && await uow.DispatchDayAsync(day.Id, nowUtc) == null)
        {
            throw new DriverNotAssignedException();
        }
        await statusEngine.SyncOrderWithDaysAsync(uow, order, days, actor.UserId, nowUtc);
        await uow.CommitAsync();
    }

    public async Task CancelDayByAdminAsync(Guid orderDayId, string reason, OrderActor actor)
    {
        var cleanReason = RequireReason(reason);
        var nowUtc = DateTime.UtcNow;
        var dayRef = await orderRepository.GetDayByIdAsync(orderDayId)
            ?? throw new OrderNotFoundException("Hari kirim tidak ditemukan.");

        await using var uow = await unitOfWorkFactory.BeginAsync();
        var order = await uow.LockOrderAsync(dayRef.OrderId)
            ?? throw new OrderNotFoundException();
        var days = await uow.GetDaysAsync(order.Id);
        var day = days.First(d => d.Id == orderDayId);

        if (!OrderDayStatusCodes.AdminCancellable.Contains(day.StatusCode))
        {
            throw new InvalidStatusTransitionException(
                day.StatusCode == OrderDayStatusCodes.Scheduled
                    ? "Pesanan belum dibayar. Batalkan pesanan lewat CancelOrderByAdmin."
                    : $"Hari kirim dengan status {day.StatusCode} tidak dapat dibatalkan.");
        }

        await statusEngine.ChangeDayStatusAsync(uow, day, OrderDayStatusCodes.CancelledByAdmin, actor, cleanReason, nowUtc,
            requiresManualRefund: true);
        await statusEngine.SyncOrderWithDaysAsync(uow, order, days, actor.UserId, nowUtc);
        await uow.CommitAsync();
    }

    public async Task CancelOrderByAdminAsync(Guid orderId, string reason, OrderActor actor)
    {
        var cleanReason = RequireReason(reason);
        var nowUtc = DateTime.UtcNow;
        string? unpaidOrderNumber = null;

        await using (var uow = await unitOfWorkFactory.BeginAsync())
        {
            var order = await uow.LockOrderAsync(orderId)
                ?? throw new OrderNotFoundException();
            var days = await uow.GetDaysAsync(order.Id);

            if (order.StatusCode == OrderStatusCodes.AwaitingPayment)
            {
                await statusEngine.CloseUnpaidOrderAsync(uow, order, days, OrderStatusCodes.CancelledByAdmin, actor, cleanReason, nowUtc);
                unpaidOrderNumber = order.OrderNumber;
            }
            else if (order.StatusCode is OrderStatusCodes.Paid or OrderStatusCodes.InProgress)
            {
                // Force majeure: semua hari yang belum selesai dibatalkan, refund manual di luar sistem
                var cancellable = days.Where(d => OrderDayStatusCodes.AdminCancellable.Contains(d.StatusCode)).ToList();
                if (cancellable.Count == 0)
                {
                    throw new InvalidStatusTransitionException("Tidak ada hari kirim yang masih bisa dibatalkan.");
                }

                foreach (var day in cancellable)
                {
                    await statusEngine.ChangeDayStatusAsync(uow, day, OrderDayStatusCodes.CancelledByAdmin, actor, cleanReason, nowUtc,
                        requiresManualRefund: true);
                }

                await statusEngine.SyncOrderWithDaysAsync(uow, order, days, actor.UserId, nowUtc);
            }
            else
            {
                throw new InvalidStatusTransitionException($"Pesanan dengan status {order.StatusCode} tidak dapat dibatalkan.");
            }

            await uow.CommitAsync();
        }

        if (unpaidOrderNumber != null)
        {
            await paymentGateway.TryExpireTransactionAsync(unpaidOrderNumber);
        }
    }

    private async Task<List<DailyOrderDayDto>> LoadDailyAsync(DateOnly date, IReadOnlySet<string> statuses, string? fulfillmentType)
    {
        var rows = (await orderRepository.GetDaysByDeliveryDateAsync(OrderMapper.ToDbDate(date), statuses.ToList(), fulfillmentType)).ToList();
        var items = (await orderRepository.GetItemsByDayIdsAsync(rows.Select(r => r.OrderDayId).ToList()))
            .GroupBy(i => i.OrderDayId)
            .ToDictionary(g => g.Key, g => g.Select(OrderMapper.ToDto).ToList());

        return rows.Select(r => new DailyOrderDayDto(
            OrderDayId: r.OrderDayId,
            OrderId: r.OrderId,
            OrderNumber: r.OrderNumber,
            CustomerName: r.CustomerName,
            FulfillmentType: r.FulfillmentType,
            StatusCode: r.StatusCode,
            Address: OrderMapper.DeserializeAddress(r.AddressSnapshot),
            Notes: r.Notes,
            Items: items.TryGetValue(r.OrderDayId, out var list) ? list : [],
            ZoneName: r.ZoneName,
            DriverUserId: r.DriverUserId,
            DriverName: r.DriverName,
            AssignmentSource: r.AssignmentSource)).ToList();
    }

    private static string RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOrderException("Alasan pembatalan wajib diisi.");
        }

        var clean = reason.Trim();
        return clean.Length > 500 ? clean[..500] : clean;
    }
}
