using Bubox.Application.DTOs.Order;
using Bubox.Application.DTOs.Settings;
using Bubox.Application.Services;

namespace Bubox.Application.Interfaces;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateOrderAsync(OrderActor actor, CreateOrderRequest request);
    Task<IReadOnlyList<OrderSummaryDto>> GetMyOrdersAsync(Guid userId);
    Task<OrderDetailDto> GetOrderDetailAsync(Guid orderId, OrderActor actor);

    /// <summary>Status ringan untuk polling halaman pembayaran QRIS.</summary>
    Task<OrderPaymentStatusDto> GetPaymentStatusAsync(Guid orderId, OrderActor actor);
    Task CancelOrderAsync(Guid orderId, OrderActor actor, string? reason);
}

public interface IOrderAdminService
{
    Task<PagedResult<OrderSummaryDto>> SearchOrdersAsync(OrderSearchRequest request);
    Task<DailyProductionDto> GetDailyProductionAsync(DateOnly date);
    Task<DailyDeliveriesDto> GetDailyDeliveriesAsync(DateOnly date);
    Task UpdateDayStatusAsync(Guid orderDayId, string statusCode, OrderActor actor);
    Task CancelDayByAdminAsync(Guid orderDayId, string reason, OrderActor actor);
    Task CancelOrderByAdminAsync(Guid orderId, string reason, OrderActor actor);
}

public interface IPaymentService
{
    Task HandleMidtransNotificationAsync(string rawPayload, CancellationToken cancellationToken = default);
    Task<int> ExpireOverdueOrdersAsync(CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<OrderSettings> GetEffectiveOrderSettingsAsync();
    Task<OrderSettingsDto> GetOrderSettingsAsync();
    Task<OrderSettingsDto> UpdateOrderSettingsAsync(UpdateOrderSettingsRequest request, Guid updatedBy);
    Task<OrderCutoffInfoDto> GetOrderCutoffInfoAsync();
}
