using Bubox.Application.DTOs.MenuCategory;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class MenuCategoryService(IMenuCategoryRepository categoryRepository) : IMenuCategoryService
{
    public async Task<IEnumerable<MenuCategoryDto>> GetCategoriesAsync(string? categoryType = null, bool includeInactive = false)
    {
        var typeFilter = string.IsNullOrWhiteSpace(categoryType) ? null : categoryType.Trim().ToUpperInvariant();
        var categories = await categoryRepository.GetAllAsync(typeFilter, includeInactive);
        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<MenuCategoryDto>> GetMainCategoriesAsync()
    {
        var categories = await categoryRepository.GetAllAsync(MenuCategoryTypes.Utama, includeInactive: false);
        return categories.Select(MapToDto);
    }

    public async Task<MenuCategoryDto?> GetByIdAsync(Guid id)
    {
        var category = await categoryRepository.GetByIdAsync(id);
        return category != null ? MapToDto(category) : null;
    }

    public async Task<Guid> CreateCategoryAsync(CreateMenuCategoryRequest request)
    {
        ValidateInput(request.Code, request.Name, request.CategoryType);

        var normalizedCode = request.Code.Trim().ToUpperInvariant().Replace(" ", "_");
        var existing = await categoryRepository.GetByCodeAsync(normalizedCode);
        if (existing != null)
        {
            throw new InvalidMenuDataException($"Kode kategori '{normalizedCode}' sudah digunakan.");
        }

        var category = new MenuCategory
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            CategoryType = request.CategoryType.Trim().ToUpperInvariant(),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await categoryRepository.CreateAsync(category);
    }

    public async Task UpdateCategoryAsync(Guid id, UpdateMenuCategoryRequest request)
    {
        var category = await categoryRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException("Kategori menu tidak ditemukan.");

        ValidateInput(category.Code, request.Name, request.CategoryType);

        var normalizedType = request.CategoryType.Trim().ToUpperInvariant();
        if (!string.Equals(category.CategoryType, normalizedType, StringComparison.OrdinalIgnoreCase))
        {
            var isAssigned = await categoryRepository.HasAssignedMenusAsync(id);
            if (isAssigned)
            {
                throw new InvalidMenuDataException("Tipe kategori tidak dapat diubah karena terdapat menu pada kategori ini yang sedang aktif terdaftar pada jadwal hari.");
            }
        }

        category.Name = request.Name.Trim();
        category.CategoryType = normalizedType;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await categoryRepository.UpdateAsync(category);
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        var category = await categoryRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException("Kategori menu tidak ditemukan.");

        var hasMenus = await categoryRepository.HasActiveMenusAsync(id);
        if (hasMenus)
        {
            throw new InvalidMenuDataException("Kategori tidak dapat dihapus karena masih digunakan oleh Master Menu aktif. Pindahkan atau hapus menu terlebih dahulu.");
        }

        await categoryRepository.SoftDeleteAsync(id);
    }

    private static void ValidateInput(string code, string name, string categoryType)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidMenuDataException("Kode kategori tidak boleh kosong.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidMenuDataException("Nama kategori tidak boleh kosong.");
        }

        var typeUpper = categoryType?.Trim().ToUpperInvariant();
        if (typeUpper != MenuCategoryTypes.Utama && typeUpper != MenuCategoryTypes.Sekunder)
        {
            throw new InvalidMenuDataException("Tipe kategori harus bernilai 'UTAMA' atau 'SEKUNDER'.");
        }
    }

    private static MenuCategoryDto MapToDto(MenuCategory c) => new(
        Id: c.Id,
        Code: c.Code,
        Name: c.Name,
        CategoryType: c.CategoryType,
        DisplayOrder: c.DisplayOrder,
        IsActive: c.IsActive,
        CreatedAt: c.CreatedAt,
        UpdatedAt: c.UpdatedAt
    );
}
