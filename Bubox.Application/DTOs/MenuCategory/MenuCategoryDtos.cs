namespace Bubox.Application.DTOs.MenuCategory;

public record MenuCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string CategoryType,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateMenuCategoryRequest(
    string Code,
    string Name,
    string CategoryType,
    int DisplayOrder = 1,
    bool IsActive = true
);

public record UpdateMenuCategoryRequest(
    string Name,
    string CategoryType,
    int DisplayOrder = 1,
    bool IsActive = true
);
