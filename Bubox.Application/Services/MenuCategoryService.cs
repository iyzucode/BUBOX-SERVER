using System.Text.RegularExpressions;
using Bubox.Application.DTOs.MenuCategory;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public partial class MenuCategoryService(IMenuCategoryRepository categoryRepository) : IMenuCategoryService
{
    private const int MaxCodeLength = 30;
    private const int MaxNameLength = 50;

    [GeneratedRegex("[^A-Z0-9]+")]
    private static partial Regex NonCodeCharacters();

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
        ValidateInput(request.Name, request.CategoryType);
        await EnsureNameAvailableAsync(request.Name, null);

        var normalizedCode = await ResolveCodeAsync(request.Code, request.Name);

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

        ValidateInput(request.Name, request.CategoryType);
        await EnsureNameAvailableAsync(request.Name, id);

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

    /// <summary>Nama kategori unik (tanpa membedakan huruf besar/kecil), termasuk kategori yang dinonaktifkan.</summary>
    private async Task EnsureNameAvailableAsync(string name, Guid? excludeId)
    {
        var trimmed = name.Trim();
        var all = await categoryRepository.GetAllAsync(null, includeInactive: true);
        if (all.Any(c => c.Id != excludeId && string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidMenuDataException($"Kategori '{trimmed}' sudah ada.");
        }
    }

    /// <summary>
    /// Kode dari isian admin, atau dibuat otomatis dari nama ("Nasi Tim" → NASI_TIM). Kode otomatis diberi akhiran
    /// bila sudah terpakai, termasuk oleh kategori yang sudah dihapus.
    /// </summary>
    private async Task<string> ResolveCodeAsync(string? requestedCode, string name)
    {
        if (!string.IsNullOrWhiteSpace(requestedCode))
        {
            var explicitCode = ToCode(requestedCode);
            if (explicitCode.Length == 0)
            {
                throw new InvalidMenuDataException("Kode kategori hanya boleh berisi huruf dan angka.");
            }
            if (await categoryRepository.CodeExistsAsync(explicitCode))
            {
                throw new InvalidMenuDataException($"Kode kategori '{explicitCode}' sudah digunakan.");
            }
            return explicitCode;
        }

        var baseCode = ToCode(name);
        if (baseCode.Length == 0) baseCode = "KATEGORI";

        var code = baseCode;
        for (var suffix = 2; await categoryRepository.CodeExistsAsync(code); suffix++)
        {
            var tail = $"_{suffix}";
            code = baseCode[..Math.Min(baseCode.Length, MaxCodeLength - tail.Length)].TrimEnd('_') + tail;
        }
        return code;
    }

    private static string ToCode(string value)
    {
        var code = NonCodeCharacters().Replace(value.Trim().ToUpperInvariant(), "_").Trim('_');
        return code.Length > MaxCodeLength ? code[..MaxCodeLength].TrimEnd('_') : code;
    }

    private static void ValidateInput(string name, string categoryType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidMenuDataException("Nama kategori tidak boleh kosong.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new InvalidMenuDataException($"Nama kategori maksimal {MaxNameLength} karakter.");
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
