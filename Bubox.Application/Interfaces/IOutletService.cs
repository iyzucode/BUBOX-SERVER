using Bubox.Application.DTOs.Outlet;

namespace Bubox.Application.Interfaces;

public interface IOutletService
{
    /// <summary>Outlet aktif untuk checkout. <paramref name="userId"/> null = belum masuk (tanpa outlet terakhir).</summary>
    Task<AvailableOutletsDto> GetAvailableAsync(Guid? userId);

    Task<IReadOnlyList<OutletDto>> GetAllAsync();
    Task<OutletDto> CreateAsync(UpsertOutletRequest request, Guid adminId);
    Task<OutletDto> UpdateAsync(Guid id, UpsertOutletRequest request, Guid adminId);
    Task<OutletDto> SetActiveAsync(Guid id, bool isActive, Guid adminId);
    Task DeleteAsync(Guid id);
}
