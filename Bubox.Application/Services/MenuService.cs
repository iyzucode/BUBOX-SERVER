using System.Text;
using Bubox.Application.DTOs.Menu;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Bubox.Application.Services;

public class MenuService(
    IMenuRepository menuRepository,
    IMenuCategoryRepository categoryRepository,
    IMenuAssignmentRepository assignmentRepository,
    IConfiguration configuration
) : IMenuService
{
    public async Task<IEnumerable<MasterMenuDto>> GetMasterMenusAsync(Guid? categoryId = null, string? search = null, bool includeInactive = false)
    {
        var menus = await menuRepository.GetAllMasterMenusAsync(categoryId, search, includeInactive);
        var categories = (await categoryRepository.GetAllAsync(includeInactive: true)).ToDictionary(c => c.Id);

        var result = new List<MasterMenuDto>();
        foreach (var m in menus)
        {
            categories.TryGetValue(m.CategoryId, out var cat);
            result.Add(new MasterMenuDto(
                Id: m.Id,
                CategoryId: m.CategoryId,
                CategoryCode: cat?.Code ?? string.Empty,
                CategoryName: cat?.Name ?? "Uncategorized",
                CategoryType: cat?.CategoryType ?? MenuCategoryTypes.Utama,
                Name: m.Name,
                Description: m.Description,
                Ingredients: m.Ingredients,
                NutritionInfo: m.NutritionInfo,
                Price: m.Price,
                ImageUrl: m.ImageUrl,
                IsActive: m.IsActive,
                CreatedAt: m.CreatedAt,
                UpdatedAt: m.UpdatedAt
            ));
        }

        return result;
    }

    public async Task<MasterMenuDto?> GetMasterMenuByIdAsync(Guid id)
    {
        var menu = await menuRepository.GetByIdAsync(id);
        if (menu == null) return null;

        var category = await categoryRepository.GetByIdAsync(menu.CategoryId);

        return new MasterMenuDto(
            Id: menu.Id,
            CategoryId: menu.CategoryId,
            CategoryCode: category?.Code ?? string.Empty,
            CategoryName: category?.Name ?? "Uncategorized",
            CategoryType: category?.CategoryType ?? MenuCategoryTypes.Utama,
            Name: menu.Name,
            Description: menu.Description,
            Ingredients: menu.Ingredients,
            NutritionInfo: menu.NutritionInfo,
            Price: menu.Price,
            ImageUrl: menu.ImageUrl,
            IsActive: menu.IsActive,
            CreatedAt: menu.CreatedAt,
            UpdatedAt: menu.UpdatedAt
        );
    }

    public async Task<Guid> CreateMasterMenuAsync(CreateMasterMenuRequest request)
    {
        ValidateInput(request.CategoryId, request.Name, request.Price);
        ValidateImageSize(request.ImageUrl);

        var category = await categoryRepository.GetByIdAsync(request.CategoryId)
            ?? throw new InvalidMenuDataException("Kategori menu yang dipilih tidak ditemukan.");

        var isDuplicate = await menuRepository.ExistsByNameInCategoryAsync(request.CategoryId, request.Name);
        if (isDuplicate)
        {
            throw new InvalidMenuDataException($"Menu dengan nama '{request.Name}' sudah ada pada kategori '{category.Name}'.");
        }

        var menu = new Menu
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Ingredients = string.IsNullOrWhiteSpace(request.Ingredients) ? null : request.Ingredients.Trim(),
            NutritionInfo = string.IsNullOrWhiteSpace(request.NutritionInfo) ? null : request.NutritionInfo.Trim(),
            Price = request.Price,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            IsActive = request.IsActive,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await menuRepository.CreateAsync(menu);
    }

    public async Task UpdateMasterMenuAsync(Guid id, UpdateMasterMenuRequest request)
    {
        var menu = await menuRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException("Master menu tidak ditemukan.");

        ValidateInput(request.CategoryId, request.Name, request.Price);
        ValidateImageSize(request.ImageUrl);

        var category = await categoryRepository.GetByIdAsync(request.CategoryId)
            ?? throw new InvalidMenuDataException("Kategori menu yang dipilih tidak ditemukan.");

        var isDuplicate = await menuRepository.ExistsByNameInCategoryAsync(request.CategoryId, request.Name, excludeId: id);
        if (isDuplicate)
        {
            throw new InvalidMenuDataException($"Menu dengan nama '{request.Name}' sudah ada pada kategori '{category.Name}'.");
        }

        menu.CategoryId = request.CategoryId;
        menu.Name = request.Name.Trim();
        menu.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        menu.Ingredients = string.IsNullOrWhiteSpace(request.Ingredients) ? null : request.Ingredients.Trim();
        menu.NutritionInfo = string.IsNullOrWhiteSpace(request.NutritionInfo) ? null : request.NutritionInfo.Trim();
        menu.Price = request.Price;
        menu.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        menu.IsActive = request.IsActive;
        menu.UpdatedAt = DateTime.UtcNow;

        await menuRepository.UpdateAsync(menu);
    }

    public async Task DeleteMasterMenuAsync(Guid id)
    {
        var menu = await menuRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException("Master menu tidak ditemukan.");

        var hasAssignments = await assignmentRepository.HasAssignmentsByMenuIdAsync(id);
        if (hasAssignments)
        {
            throw new InvalidMenuDataException("Menu tidak dapat dihapus karena masih aktif terdaftar pada jadwal hari. Lepaskan assignment terlebih dahulu.");
        }

        await menuRepository.SoftDeleteAsync(id);
    }

    public async Task ToggleStatusAsync(Guid id, bool isActive)
    {
        var menu = await menuRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException("Master menu tidak ditemukan.");

        await menuRepository.ToggleStatusAsync(id, isActive);
    }

    public async Task AssignMenuToDayAsync(AssignMenuRequest request)
    {
        ValidateDay(request.DayOfWeek);

        var menu = await menuRepository.GetByIdAsync(request.MenuId)
            ?? throw new MenuNotFoundException("Menu yang akan di-assign tidak ditemukan.");

        var exists = await assignmentRepository.ExistsAssignmentAsync(request.MenuId, request.DayOfWeek);
        if (exists)
        {
            throw new InvalidMenuDataException($"Menu '{menu.Name}' sudah terdaftar pada hari {MenuConstants.GetDayName(request.DayOfWeek)}.");
        }

        var assignment = new MenuAssignment
        {
            Id = Guid.NewGuid(),
            MenuId = request.MenuId,
            DayOfWeek = request.DayOfWeek,
            DayName = MenuConstants.GetDayName(request.DayOfWeek),
            CreatedAt = DateTime.UtcNow
        };

        await assignmentRepository.AssignMenuAsync(assignment);
    }

    public async Task<int> BatchAssignMenusToDayAsync(BatchAssignMenusRequest request)
    {
        ValidateDay(request.DayOfWeek);

        if (request.MenuIds == null || request.MenuIds.Count == 0)
        {
            return 0;
        }

        var dayName = MenuConstants.GetDayName(request.DayOfWeek);
        var assignments = request.MenuIds
            .Distinct()
            .Select(menuId => new MenuAssignment
            {
                Id = Guid.NewGuid(),
                MenuId = menuId,
                DayOfWeek = request.DayOfWeek,
                DayName = dayName,
                CreatedAt = DateTime.UtcNow
            });

        return await assignmentRepository.BatchAssignMenusAsync(assignments);
    }

    public async Task RemoveAssignmentAsync(Guid assignmentId)
    {
        await assignmentRepository.HardDeleteAsync(assignmentId);
    }

    public async Task RemoveAssignmentByMenuAndDayAsync(Guid menuId, int dayOfWeek)
    {
        ValidateDay(dayOfWeek);
        await assignmentRepository.HardDeleteByMenuAndDayAsync(menuId, dayOfWeek);
    }

    public async Task<DayMenuScheduleDto> GetMenuByDayAsync(int dayOfWeek)
    {
        ValidateDay(dayOfWeek);

        var assignedMenus = (await assignmentRepository.GetByDayAsync(dayOfWeek)).ToList();

        var mainMenus = assignedMenus
            .Where(m => m.CategoryType == MenuCategoryTypes.Utama)
            .Select(MapAssignedToDto)
            .ToList();

        var secondaryMenus = assignedMenus
            .Where(m => m.CategoryType == MenuCategoryTypes.Sekunder)
            .Select(MapAssignedToDto)
            .ToList();

        return new DayMenuScheduleDto(
            DayOfWeek: dayOfWeek,
            DayName: MenuConstants.GetDayName(dayOfWeek),
            MainMenus: mainMenus,
            SecondaryMenus: secondaryMenus
        );
    }

    public async Task<WeeklyMenuScheduleResponse> GetWeeklyMenuScheduleAsync()
    {
        var allAssigned = (await assignmentRepository.GetAllWeeklyAssignmentsAsync()).ToList();

        var days = new List<DayMenuScheduleDto>();
        for (int day = 1; day <= 7; day++)
        {
            var dayItems = allAssigned.Where(m => m.DayOfWeek == day).ToList();
            var main = dayItems.Where(m => m.CategoryType == MenuCategoryTypes.Utama).Select(MapAssignedToDto).ToList();
            var secondary = dayItems.Where(m => m.CategoryType == MenuCategoryTypes.Sekunder).Select(MapAssignedToDto).ToList();

            days.Add(new DayMenuScheduleDto(
                DayOfWeek: day,
                DayName: MenuConstants.GetDayName(day),
                MainMenus: main,
                SecondaryMenus: secondary
            ));
        }

        return new WeeklyMenuScheduleResponse(days, allAssigned.Count);
    }

    private static void ValidateDay(int dayOfWeek)
    {
        if (dayOfWeek < 1 || dayOfWeek > 7)
        {
            throw new InvalidMenuDataException("Hari harus bernilai antara 1 (Senin) hingga 7 (Minggu).");
        }
    }

    private static void ValidateInput(Guid categoryId, string name, decimal price)
    {
        if (categoryId == Guid.Empty)
        {
            throw new InvalidMenuDataException("Kategori menu wajib dipilih.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidMenuDataException("Nama menu tidak boleh kosong.");
        }

        if (price < 0)
        {
            throw new InvalidMenuDataException("Harga menu tidak boleh bernilai negatif.");
        }
    }

    private void ValidateImageSize(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        var maxSizeBytes = 1048576L; // Default 1MB
        var configuredStr = configuration["Menu:MaxImageSizeBytes"];
        if (long.TryParse(configuredStr, out var parsedSize) && parsedSize > 0)
        {
            maxSizeBytes = parsedSize;
        }

        long byteCount;
        var commaIndex = imageUrl.IndexOf(',');
        var base64Data = commaIndex >= 0 ? imageUrl[(commaIndex + 1)..] : imageUrl;

        try
        {
            var rawLength = base64Data.Length;
            var padding = 0;
            if (base64Data.EndsWith("==")) padding = 2;
            else if (base64Data.EndsWith("=")) padding = 1;
            byteCount = (rawLength * 3L / 4L) - padding;
        }
        catch
        {
            byteCount = Encoding.UTF8.GetByteCount(imageUrl);
        }

        if (byteCount > maxSizeBytes)
        {
            var maxMb = (double)maxSizeBytes / (1024 * 1024);
            throw new InvalidMenuDataException($"Ukuran gambar melebihi batas maksimal {maxMb:0.#} MB.");
        }
    }

    private static MenuDto MapAssignedToDto(AssignedMenuDetail m) => new(
        Id: m.MenuId,
        DayOfWeek: m.DayOfWeek,
        DayName: m.DayName,
        MenuType: m.CategoryType,
        Category: m.CategoryName,
        Name: m.Name,
        Description: m.Description,
        Ingredients: m.Ingredients,
        NutritionInfo: m.NutritionInfo,
        Price: m.Price,
        ImageUrl: m.ImageUrl,
        IsActive: m.IsActive,
        CreatedAt: m.CreatedAt,
        UpdatedAt: m.UpdatedAt,
        CategoryId: m.CategoryId,
        AssignmentId: m.AssignmentId
    );
}
