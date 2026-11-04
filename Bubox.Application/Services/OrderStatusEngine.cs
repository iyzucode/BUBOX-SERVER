using Bubox.Application.Exceptions;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

/// <summary>Siapa yang melakukan perubahan status.</summary>
public sealed record OrderActor(Guid? UserId, IReadOnlySet<string> RoleCodes)
{
    public static OrderActor System(Guid? triggeredBy = null) =>
        new(triggeredBy, new HashSet<string> { OrderActorRoles.System });

    public static OrderActor FromUser(Guid userId, IEnumerable<string> roleNames) =>
        new(userId, roleNames
            .Select(RoleConstants.ToCode)
            .Where(code => code != null)
            .Select(code => code!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase));

    public bool HasAnyRole(params string[] roleCodes) => roleCodes.Any(RoleCodes.Contains);

    public bool IsStaff => HasAnyRole(
        RoleConstants.Codes.Admin, RoleConstants.Codes.SuperAdmin, RoleConstants.Codes.Owner,
        RoleConstants.Codes.Kitchen, RoleConstants.Codes.Driver);
}

/// <summary>
/// State machine Order & OrderDay. Semua perubahan status harus lewat kelas ini, di dalam
/// <see cref="IOrderUnitOfWork"/> yang sudah mengunci order-nya.
/// Transisi yang sah dan role pelakunya dibaca dari tabel rf_order_status_transitions.
/// </summary>
public class OrderStatusEngine(IOrderStatusTransitionRepository transitionRepository)
{
    private List<OrderStatusTransition>? _transitions;

    public async Task ChangeOrderStatusAsync(
        IOrderUnitOfWork uow, Order order, string toCode, OrderActor actor, string? reason, DateTime nowUtc)
    {
        var role = await AuthorizeAsync(OrderStatusScopes.Order, order.StatusCode, toCode, actor);

        var updated = await uow.UpdateOrderStatusAsync(order.Id, order.StatusCode, toCode, nowUtc,
            cancelReason: reason, cancelledBy: actor.UserId);
        if (!updated)
        {
            throw new OrderConcurrencyException();
        }

        await uow.InsertHistoryAsync(NewHistory(order.Id, OrderStatusScopes.Order, order.Id, order.StatusCode, toCode, actor, role, reason, nowUtc));

        order.StatusCode = toCode;
        order.UpdatedAt = nowUtc;
    }

    public async Task ChangeDayStatusAsync(
        IOrderUnitOfWork uow, OrderDay day, string toCode, OrderActor actor, string? reason, DateTime nowUtc,
        bool requiresManualRefund = false)
    {
        var role = await AuthorizeAsync(OrderStatusScopes.Day, day.StatusCode, toCode, actor);

        var updated = await uow.UpdateDayStatusAsync(day.Id, day.StatusCode, toCode, nowUtc,
            cancelReason: reason, cancelledBy: actor.UserId, requiresManualRefund: requiresManualRefund);
        if (!updated)
        {
            throw new OrderConcurrencyException();
        }

        await uow.InsertHistoryAsync(NewHistory(day.OrderId, OrderStatusScopes.Day, day.Id, day.StatusCode, toCode, actor, role, reason, nowUtc));

        day.StatusCode = toCode;
        day.UpdatedAt = nowUtc;
        day.RequiresManualRefund |= requiresManualRefund;
    }

    /// <summary>Order dibayar: AWAITING_PAYMENT → PAID, semua hari SCHEDULED → CONFIRMED.</summary>
    public async Task MarkPaidAsync(IOrderUnitOfWork uow, Order order, List<OrderDay> days, DateTime nowUtc, string reason)
    {
        var system = OrderActor.System();
        await ChangeOrderStatusAsync(uow, order, OrderStatusCodes.Paid, system, reason, nowUtc);

        foreach (var day in days.Where(d => d.StatusCode == OrderDayStatusCodes.Scheduled))
        {
            await ChangeDayStatusAsync(uow, day, OrderDayStatusCodes.Confirmed, system, null, nowUtc);
        }
    }

    /// <summary>Order belum dibayar ditutup (batal/expired/gagal bayar): semua hari SCHEDULED → CANCELLED.</summary>
    public async Task CloseUnpaidOrderAsync(
        IOrderUnitOfWork uow, Order order, List<OrderDay> days, string toCode, OrderActor actor, string? reason, DateTime nowUtc)
    {
        if (order.StatusCode != OrderStatusCodes.AwaitingPayment)
        {
            throw new InvalidStatusTransitionException("Hanya pesanan yang belum dibayar yang dapat ditutup dengan cara ini.");
        }

        await ChangeOrderStatusAsync(uow, order, toCode, actor, reason, nowUtc);

        var system = OrderActor.System(actor.UserId);
        foreach (var day in days.Where(d => d.StatusCode == OrderDayStatusCodes.Scheduled))
        {
            await ChangeDayStatusAsync(uow, day, OrderDayStatusCodes.Cancelled, system, reason, nowUtc);
        }
    }

    /// <summary>
    /// Menyesuaikan status Order setelah ada perubahan status hari:
    /// hari pertama mulai diproses → IN_PROGRESS; semua hari terminal → COMPLETED / CANCELLED_BY_ADMIN.
    /// </summary>
    public async Task SyncOrderWithDaysAsync(IOrderUnitOfWork uow, Order order, List<OrderDay> days, Guid? triggeredBy, DateTime nowUtc)
    {
        if (!OrderStatusCodes.PaidOrLater.Contains(order.StatusCode) || order.StatusCode == OrderStatusCodes.Completed)
        {
            return;
        }

        var system = OrderActor.System(triggeredBy);

        if (days.Count > 0 && days.All(d => OrderDayStatusCodes.Terminal.Contains(d.StatusCode)))
        {
            var anyFulfilled = days.Any(d => OrderDayStatusCodes.Fulfilled.Contains(d.StatusCode));
            var target = anyFulfilled ? OrderStatusCodes.Completed : OrderStatusCodes.CancelledByAdmin;
            var reason = anyFulfilled ? null : "Semua hari dibatalkan karena force majeure.";
            await ChangeOrderStatusAsync(uow, order, target, system, reason, nowUtc);
            return;
        }

        if (order.StatusCode == OrderStatusCodes.Paid && days.Any(d => OrderDayStatusCodes.Started.Contains(d.StatusCode)))
        {
            await ChangeOrderStatusAsync(uow, order, OrderStatusCodes.InProgress, system, null, nowUtc);
        }
    }

    private async Task<string> AuthorizeAsync(string scope, string fromCode, string toCode, OrderActor actor)
    {
        _transitions ??= (await transitionRepository.GetAllAsync()).ToList();

        var transition = _transitions.FirstOrDefault(t =>
            t.Scope == scope && t.FromCode == fromCode && t.ToCode == toCode);

        if (transition == null)
        {
            var label = scope == OrderStatusScopes.Order ? "Pesanan" : "Hari kirim";
            throw new InvalidStatusTransitionException($"{label} dengan status {fromCode} tidak dapat diubah menjadi {toCode}.");
        }

        var matchedRole = transition.AllowedRoles.FirstOrDefault(actor.RoleCodes.Contains);
        if (matchedRole == null)
        {
            throw new ForbiddenStatusTransitionException(
                $"Perubahan {fromCode} → {toCode} hanya dapat dilakukan oleh: {string.Join(", ", transition.AllowedRoles)}.");
        }

        return matchedRole;
    }

    private static OrderStatusHistory NewHistory(
        Guid orderId, string scope, Guid entityId, string? fromCode, string toCode,
        OrderActor actor, string role, string? reason, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = orderId,
        Scope = scope,
        EntityId = entityId,
        FromCode = fromCode,
        ToCode = toCode,
        ActorUserId = actor.UserId,
        ActorRole = role,
        Reason = reason,
        CreatedAt = nowUtc
    };
}
