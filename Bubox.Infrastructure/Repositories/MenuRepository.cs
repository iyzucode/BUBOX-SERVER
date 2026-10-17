using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class MenuRepository(IDbConnectionFactory connectionFactory) : IMenuRepository
{
    private const string SelectColumns = """
        id AS Id,
        category_id AS CategoryId,
        name AS Name,
        description AS Description,
        ingredients AS Ingredients,
        nutrition_info AS NutritionInfo,
        price AS Price,
        image_url AS ImageUrl,
        is_active AS IsActive,
        is_deleted AS IsDeleted,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt,
        deleted_at AS DeletedAt
        """;

    public async Task<Guid> CreateAsync(Menu menu)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            INSERT INTO menus (
                category_id,
                name,
                description,
                ingredients,
                nutrition_info,
                price,
                image_url,
                is_active,
                is_deleted,
                created_at,
                updated_at
            )
            VALUES (
                @CategoryId,
                @Name,
                @Description,
                @Ingredients,
                @NutritionInfo,
                @Price,
                @ImageUrl,
                @IsActive,
                false,
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
            SET category_id = @CategoryId,
                name = @Name,
                description = @Description,
                ingredients = @Ingredients,
                nutrition_info = @NutritionInfo,
                price = @Price,
                image_url = @ImageUrl,
                is_active = @IsActive,
                updated_at = NOW()
            WHERE id = @Id AND is_deleted = false;
            """;

        await connection.ExecuteAsync(sql, menu);
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            UPDATE menus
            SET is_deleted = true,
                is_active = false,
                deleted_at = NOW(),
                updated_at = NOW()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task ToggleStatusAsync(Guid id, bool isActive)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            UPDATE menus
            SET is_active = @IsActive,
                updated_at = NOW()
            WHERE id = @Id AND is_deleted = false;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
    }

    public async Task<Menu?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM menus
            WHERE id = @Id AND is_deleted = false;
            """;

        return await connection.QuerySingleOrDefaultAsync<Menu>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Menu>> GetAllMasterMenusAsync(Guid? categoryId = null, string? search = null, bool includeInactive = false)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM menus
            WHERE is_deleted = false
              AND (@IncludeInactive = true OR is_active = true)
              AND (@CategoryId IS NULL OR category_id = @CategoryId)
              AND (@Search IS NULL OR LOWER(name) LIKE '%' || LOWER(@Search) || '%')
            ORDER BY created_at DESC;
            """;

        return await connection.QueryAsync<Menu>(sql, new
        {
            CategoryId = categoryId,
            Search = search,
            IncludeInactive = includeInactive
        });
    }

    public async Task<bool> ExistsByNameInCategoryAsync(Guid categoryId, string name, Guid? excludeId = null)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            SELECT EXISTS (
                SELECT 1
                FROM menus
                WHERE category_id = @CategoryId
                  AND LOWER(TRIM(name)) = LOWER(TRIM(@Name))
                  AND is_deleted = false
                  AND (@ExcludeId IS NULL OR id != @ExcludeId)
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql, new
        {
            CategoryId = categoryId,
            Name = name,
            ExcludeId = excludeId
        });
    }
}
