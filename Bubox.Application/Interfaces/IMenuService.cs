using Bubox.Application.DTOs.Menu;

namespace Bubox.Application.Interfaces;

public interface IMenuService
{
    // Master Menu Catalog
    Task<IEnumerable<MasterMenuDto>> GetMasterMenusAsync(Guid? categoryId = null, string? search = null, bool includeInactive = false);
    Task<MasterMenuDto?> GetMasterMenuByIdAsync(Guid id);
    Task<Guid> CreateMasterMenuAsync(CreateMasterMenuRequest request);
    Task UpdateMasterMenuAsync(Guid id, UpdateMasterMenuRequest request);
    Task DeleteMasterMenuAsync(Guid id);
    Task ToggleStatusAsync(Guid id, bool isActive);

    // Day Assignments & Schedule
    Task AssignMenuToDayAsync(AssignMenuRequest request);
    Task<int> BatchAssignMenusToDayAsync(BatchAssignMenusRequest request);
    Task RemoveAssignmentAsync(Guid assignmentId);
    Task RemoveAssignmentByMenuAndDayAsync(Guid menuId, int dayOfWeek);
    Task<DayMenuScheduleDto> GetMenuByDayAsync(int dayOfWeek);
    Task<WeeklyMenuScheduleResponse> GetWeeklyMenuScheduleAsync();
}
