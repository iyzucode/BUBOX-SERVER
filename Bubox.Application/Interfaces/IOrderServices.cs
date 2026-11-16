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
    /// <summary>Dapur: hari kirim langsung jadi READY (CONFIRMED → PREPARING → READY). Yang sudah READY+ dilewati.</summary>
    Task MarkDayReadyAsync(Guid orderDayId, OrderActor actor);
    Task CancelDayByAdminAsync(Guid orderDayId, string reason, OrderActor actor);
    Task CancelOrderByAdminAsync(Guid orderId, string reason, OrderActor actor);
}

/// <summary>Layar admin Pesanan: daftar per hari kirim / per pesanan dengan filter, tab, ringkasan, & detail.</summary>
public interface IAdminOrderBoardService
{
    Task<AdminBoardResult<AdminDayRowDto>> SearchDaysAsync(AdminOrderBoardRequest request);
    Task<AdminBoardResult<AdminOrderRowDto>> SearchOrdersAsync(AdminOrderBoardRequest request);
    Task<AdminOrderDetailDto> GetOrderDetailAsync(Guid orderId, OrderActor actor);

    // Aksi massal (tiap hari diproses sendiri; hasil berisi yang gagal beserta alasannya)
    Task<BulkActionResultDto> BulkUpdateDayStatusAsync(BulkDayStatusRequest request, OrderActor actor);
    Task<BulkActionResultDto> BulkCancelDaysAsync(BulkCancelDaysRequest request, OrderActor actor);
    Task<BulkActionResultDto> MarkDaysRefundedAsync(MarkDaysRefundedRequest request, Guid adminId);

    // Ekspor Excel & lembar dapur (mengikuti filter, tab, dan urutan yang sama dengan daftar)
    Task<AdminExportFile> ExportDaysAsync(AdminOrderBoardRequest request);
    Task<AdminExportFile> ExportOrdersAsync(AdminOrderBoardRequest request);
    Task<KitchenSheetDto> GetKitchenSheetAsync(AdminOrderBoardRequest request);

    /// <summary>Jumlah pesanan baru & baru dibayar sejak waktu tertentu (indikator pesanan baru).</summary>
    Task<BoardActivityDto> GetActivityAsync(DateTime sinceUtc);
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
