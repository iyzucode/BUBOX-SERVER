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
    DateTime UpdatedAt,
    Guid CategoryId = default,
    Guid? AssignmentId = null
);

public record MasterMenuDto(
    Guid Id,
    Guid CategoryId,
    string CategoryCode,
    string CategoryName,
    string CategoryType,
    string Name,
    string? Description,
    string? Ingredients,
    string? NutritionInfo,
    decimal Price,
    string? ImageUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int AssignedDaysCount = 0
);

public record CreateMasterMenuRequest(
    Guid CategoryId,
    string Name,
    decimal Price,
    string? Description = null,
    string? Ingredients = null,
    string? NutritionInfo = null,
    string? ImageUrl = null,
    bool IsActive = true
);

public record UpdateMasterMenuRequest(
    Guid CategoryId,
    string Name,
    decimal Price,
    string? Description = null,
    string? Ingredients = null,
    string? NutritionInfo = null,
    string? ImageUrl = null,
    bool IsActive = true
);

public record AssignMenuRequest(
    Guid MenuId,
    int DayOfWeek
);

public record BatchAssignMenusRequest(
    int DayOfWeek,
    List<Guid> MenuIds
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
