using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class MenuRepository(IDbConnectionFactory connectionFactory) : IMenuRepository
{
    private const string SelectColumns = """
        id AS Id,
        day_of_week AS DayOfWeek,
        day_name AS DayName,
        menu_type AS MenuType,
        category AS Category,
        name AS Name,
        description AS Description,
        ingredients AS Ingredients,
        nutrition_info AS NutritionInfo,
        price AS Price,
        image_url AS ImageUrl,
        is_active AS IsActive,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt
        """;

    public async Task<Guid> CreateAsync(Menu menu)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            INSERT INTO menus (
                day_of_week,
                day_name,
                menu_type,
                category,
                name,
                description,
                ingredients,
                nutrition_info,
                price,
                image_url,
                is_active,
                created_at,
                updated_at
            )
            VALUES (
                @DayOfWeek,
                @DayName,
                @MenuType,
                @Category,
                @Name,
                @Description,
                @Ingredients,
                @NutritionInfo,
                @Price,
                @ImageUrl,
                @IsActive,
                NOW(),
                NOW()
            )
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<Guid>(sql, menu);
    }

    public async Task UpdateAsync(Menu menu)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            UPDATE menus
            SET day_of_week = @DayOfWeek,
                day_name = @DayName,
                menu_type = @MenuType,
                category = @Category,
                name = @Name,
                description = @Description,
                ingredients = @Ingredients,
                nutrition_info = @NutritionInfo,
                price = @Price,
                image_url = @ImageUrl,
                is_active = @IsActive,
                updated_at = NOW()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, menu);
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = "DELETE FROM menus WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task ToggleStatusAsync(Guid id, bool isActive)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            UPDATE menus
            SET is_active = @IsActive,
                updated_at = NOW()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
    }

    public async Task<Menu?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM menus
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<Menu>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Menu>> GetByDayAsync(int dayOfWeek, string? menuType = null)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM menus
            WHERE day_of_week = @DayOfWeek
              AND (@MenuType IS NULL OR menu_type = @MenuType)
            ORDER BY menu_type DESC, name ASC;
            """;

        return await connection.QueryAsync<Menu>(sql, new { DayOfWeek = dayOfWeek, MenuType = menuType });
    }

    public async Task<IEnumerable<Menu>> GetAllWeeklyMenusAsync(string? menuType = null)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM menus
            WHERE (@MenuType IS NULL OR menu_type = @MenuType)
            ORDER BY day_of_week ASC, menu_type DESC, name ASC;
            """;

        return await connection.QueryAsync<Menu>(sql, new { MenuType = menuType });
    }

    public async Task<IEnumerable<Menu>> GetByCategoryAsync(string category)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM menus
            WHERE category = @Category
            ORDER BY day_of_week ASC, name ASC;
            """;

        return await connection.QueryAsync<Menu>(sql, new { Category = category });
    }
}
