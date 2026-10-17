using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IMenuCategoryRepository
{
    Task<IEnumerable<MenuCategory>> GetAllAsync(string? categoryType = null, bool includeInactive = false);
    Task<MenuCategory?> GetByIdAsync(Guid id);
    Task<MenuCategory?> GetByCodeAsync(string code);
    Task<bool> HasActiveMenusAsync(Guid categoryId);
    Task<bool> HasAssignedMenusAsync(Guid categoryId);
    Task<Guid> CreateAsync(MenuCategory category);
    Task UpdateAsync(MenuCategory category);
    Task SoftDeleteAsync(Guid id);
}
