using Bubox.Application.DTOs.Menu;

namespace Bubox.Application.Interfaces;

public interface IMenuService
{
    Task<Guid> CreateMenuAsync(CreateMenuRequest request);
    Task UpdateMenuAsync(Guid id, UpdateMenuRequest request);
    Task DeleteMenuAsync(Guid id);
    Task ToggleStatusAsync(Guid id, bool isActive);
    Task<MenuDto?> GetMenuByIdAsync(Guid id);
    Task<DayMenuScheduleDto> GetMenuByDayAsync(int dayOfWeek);
    Task<WeeklyMenuScheduleResponse> GetWeeklyMenuScheduleAsync();
}
