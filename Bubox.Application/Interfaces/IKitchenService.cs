using Bubox.Application.DTOs.Kitchen;
using Bubox.Application.DTOs.Order;
using Bubox.Application.Services;

namespace Bubox.Application.Interfaces;

/// <summary>Validasi & rilis daftar masak (admin, H-1) dan papan dapur (hari H).</summary>
public interface IKitchenService
{
    Task<KitchenValidationDto> GetValidationAsync(DateOnly date);
    Task<KitchenValidationDto> ReleaseAsync(DateOnly date, Guid adminId);
    Task CancelReleaseAsync(DateOnly date);

    Task<KitchenBoardDto> GetBoardAsync(DateOnly date);
    /// <summary>Tandai paket siap (READY). Hanya untuk tanggal yang sudah dirilis.</summary>
    Task<BulkActionResultDto> MarkReadyAsync(DateOnly date, IReadOnlyCollection<Guid> orderDayIds, OrderActor actor);
}
