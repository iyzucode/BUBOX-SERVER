using Bubox.Application.DTOs.MenuCategory;

namespace Bubox.Application.Interfaces;

public interface IMenuCategoryService
{
    Task<IEnumerable<MenuCategoryDto>> GetCategoriesAsync(string? categoryType = null, bool includeInactive = false);
    Task<IEnumerable<MenuCategoryDto>> GetMainCategoriesAsync();
    Task<MenuCategoryDto?> GetByIdAsync(Guid id);
    Task<Guid> CreateCategoryAsync(CreateMenuCategoryRequest request);
    Task UpdateCategoryAsync(Guid id, UpdateMenuCategoryRequest request);
    Task DeleteCategoryAsync(Guid id);
}
