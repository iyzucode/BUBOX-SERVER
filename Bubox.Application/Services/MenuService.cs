using System.Text;
using Bubox.Application.DTOs.Menu;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Bubox.Application.Services;

public class MenuService(IMenuRepository menuRepository, IConfiguration configuration) : IMenuService
{
    public async Task<Guid> CreateMenuAsync(CreateMenuRequest request)
    {
        ValidateMenuInput(request.DayOfWeek, request.MenuType, request.Name, request.Price);
        ValidateImageSize(request.ImageUrl);

        var menu = new Menu
        {
            Id = Guid.NewGuid(),
            DayOfWeek = request.DayOfWeek,
            DayName = MenuConstants.GetDayName(request.DayOfWeek),
            MenuType = request.MenuType.Trim().ToUpperInvariant(),
            Category = request.Category.Trim(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Ingredients = string.IsNullOrWhiteSpace(request.Ingredients) ? null : request.Ingredients.Trim(),
            NutritionInfo = string.IsNullOrWhiteSpace(request.NutritionInfo) ? null : request.NutritionInfo.Trim(),
            Price = request.Price,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await menuRepository.CreateAsync(menu);
    }

    public async Task UpdateMenuAsync(Guid id, UpdateMenuRequest request)
    {
        var existing = await menuRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException();

        ValidateMenuInput(request.DayOfWeek, request.MenuType, request.Name, request.Price);
        ValidateImageSize(request.ImageUrl);

        existing.DayOfWeek = request.DayOfWeek;
        existing.DayName = MenuConstants.GetDayName(request.DayOfWeek);
        existing.MenuType = request.MenuType.Trim().ToUpperInvariant();
        existing.Category = request.Category.Trim();
        existing.Name = request.Name.Trim();
        existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        existing.Ingredients = string.IsNullOrWhiteSpace(request.Ingredients) ? null : request.Ingredients.Trim();
        existing.NutritionInfo = string.IsNullOrWhiteSpace(request.NutritionInfo) ? null : request.NutritionInfo.Trim();
        existing.Price = request.Price;
        existing.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        existing.IsActive = request.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await menuRepository.UpdateAsync(existing);
    }

    public async Task DeleteMenuAsync(Guid id)
    {
        var existing = await menuRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException();

        await menuRepository.DeleteAsync(existing.Id);
    }

    public async Task ToggleStatusAsync(Guid id, bool isActive)
    {
        var existing = await menuRepository.GetByIdAsync(id)
            ?? throw new MenuNotFoundException();

        await menuRepository.ToggleStatusAsync(existing.Id, isActive);
    }

    public async Task<MenuDto?> GetMenuByIdAsync(Guid id)
    {
        var menu = await menuRepository.GetByIdAsync(id);
        return menu != null ? MapToDto(menu) : null;
    }

    public async Task<DayMenuScheduleDto> GetMenuByDayAsync(int dayOfWeek)
    {
        if (dayOfWeek < 1 || dayOfWeek > 7)
        {
            throw new InvalidMenuDataException("Hari harus berada dalam rentang 1 (Senin) s/d 7 (Minggu).");
        }

        var menus = (await menuRepository.GetByDayAsync(dayOfWeek)).ToList();
        var mainMenus = menus.Where(m => m.MenuType == MenuConstants.Types.Utama).Select(MapToDto).ToList();
        var secondaryMenus = menus.Where(m => m.MenuType == MenuConstants.Types.Sekunder).Select(MapToDto).ToList();

        return new DayMenuScheduleDto(
            DayOfWeek: dayOfWeek,
            DayName: MenuConstants.GetDayName(dayOfWeek),
            MainMenus: mainMenus,
            SecondaryMenus: secondaryMenus
        );
    }

    public async Task<WeeklyMenuScheduleResponse> GetWeeklyMenuScheduleAsync()
    {
        var allMenus = (await menuRepository.GetAllWeeklyMenusAsync()).ToList();

        var days = new List<DayMenuScheduleDto>();
        for (int day = 1; day <= 7; day++)
        {
            var dayMenus = allMenus.Where(m => m.DayOfWeek == day).ToList();
            var main = dayMenus.Where(m => m.MenuType == MenuConstants.Types.Utama).Select(MapToDto).ToList();
            var secondary = dayMenus.Where(m => m.MenuType == MenuConstants.Types.Sekunder).Select(MapToDto).ToList();

            days.Add(new DayMenuScheduleDto(
                DayOfWeek: day,
                DayName: MenuConstants.GetDayName(day),
                MainMenus: main,
                SecondaryMenus: secondary
            ));
        }

        return new WeeklyMenuScheduleResponse(days, allMenus.Count);
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

    private static void ValidateMenuInput(int dayOfWeek, string menuType, string name, decimal price)
    {
        if (dayOfWeek < 1 || dayOfWeek > 7)
        {
            throw new InvalidMenuDataException("Hari harus bernilai antara 1 (Senin) hingga 7 (Minggu).");
        }

        var typeUpper = menuType?.Trim().ToUpperInvariant();
        if (typeUpper != MenuConstants.Types.Utama && typeUpper != MenuConstants.Types.Sekunder)
        {
            throw new InvalidMenuDataException("Tipe menu harus bernilai 'UTAMA' atau 'SEKUNDER'.");
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

    private static MenuDto MapToDto(Menu m) => new(
        Id: m.Id,
        DayOfWeek: m.DayOfWeek,
        DayName: m.DayName,
        MenuType: m.MenuType,
        Category: m.Category,
        Name: m.Name,
        Description: m.Description,
        Ingredients: m.Ingredients,
        NutritionInfo: m.NutritionInfo,
        Price: m.Price,
        ImageUrl: m.ImageUrl,
        IsActive: m.IsActive,
        CreatedAt: m.CreatedAt,
        UpdatedAt: m.UpdatedAt
    );
}
