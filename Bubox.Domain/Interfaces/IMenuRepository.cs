using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IMenuRepository
{
    Task<Guid> CreateAsync(Menu menu);
    Task UpdateAsync(Menu menu);
    Task DeleteAsync(Guid id);
    Task ToggleStatusAsync(Guid id, bool isActive);
    Task<Menu?> GetByIdAsync(Guid id);
    Task<IEnumerable<Menu>> GetByDayAsync(int dayOfWeek, string? menuType = null);
    Task<IEnumerable<Menu>> GetAllWeeklyMenusAsync(string? menuType = null);
    Task<IEnumerable<Menu>> GetByCategoryAsync(string category);
}
