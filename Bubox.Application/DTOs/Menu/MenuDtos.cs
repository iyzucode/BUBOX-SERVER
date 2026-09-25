namespace Bubox.Application.DTOs.Menu;

public record MenuDto(
    Guid Id,
    int DayOfWeek,
    string DayName,
    string MenuType,
    string Category,
    string Name,
    string? Description,
    string? Ingredients,
    string? NutritionInfo,
    decimal Price,
    string? ImageUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateMenuRequest(
    int DayOfWeek,
    string MenuType,
    string Category,
    string Name,
    string? Description,
    string? Ingredients,
    string? NutritionInfo,
    decimal Price,
    string? ImageUrl,
    bool IsActive = true
);

public record UpdateMenuRequest(
    int DayOfWeek,
    string MenuType,
    string Category,
    string Name,
    string? Description,
    string? Ingredients,
    string? NutritionInfo,
    decimal Price,
    string? ImageUrl,
    bool IsActive = true
);

public record DayMenuScheduleDto(
    int DayOfWeek,
    string DayName,
    List<MenuDto> MainMenus,
    List<MenuDto> SecondaryMenus
);

public record WeeklyMenuScheduleResponse(
    List<DayMenuScheduleDto> Days,
    int TotalMenus
);
