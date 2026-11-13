using Bubox.Application.DTOs.Delivery;

namespace Bubox.Application.Interfaces;

/// <summary>Pengaturan admin: area pengantaran, zona ("driver wilayah"), driver, dan penugasan manual.</summary>
public interface IDeliveryAdminService
{
    Task<AreaChildrenDto> GetAreaChildrenAsync(string? parentCode);
    Task<IReadOnlyList<WilayahSearchItemDto>> SearchWilayahAsync(string query);
    Task<AreaNodeDto> SetCoverageAsync(SetCoverageRequest request, Guid adminId);
    Task<AreaNodeDto> SetZoneAreaAsync(SetZoneAreaRequest request, Guid adminId);

    Task<IReadOnlyList<DeliveryZoneDto>> GetZonesAsync();
    Task<DeliveryZoneDto> CreateZoneAsync(UpsertZoneRequest request, Guid adminId);
    Task<DeliveryZoneDto> UpdateZoneAsync(Guid id, UpsertZoneRequest request, Guid adminId);
    Task<DeliveryZoneDto> SetZoneDriverAsync(Guid zoneId, SetZoneDriverRequest request, Guid adminId);
    Task<IReadOnlyList<ZoneDriverHistoryDto>> GetZoneDriverHistoryAsync(Guid zoneId);
    Task<IReadOnlyList<ZoneAreaDto>> GetZoneAreasAsync(Guid zoneId);
    Task DeleteZoneAsync(Guid zoneId);

    Task<IReadOnlyList<DriverDto>> GetDriversAsync();
    Task<IReadOnlyList<UserSearchDto>> SearchUsersAsync(string query);
    Task GrantDriverAsync(Guid userId);
    Task RevokeDriverAsync(Guid userId, Guid adminId);

    Task<SetDayDriverResponse> SetDayDriverAsync(SetDayDriverRequest request, Guid adminId);
}
