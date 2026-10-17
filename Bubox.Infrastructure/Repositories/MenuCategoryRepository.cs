using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class MenuCategoryRepository(IDbConnectionFactory connectionFactory) : IMenuCategoryRepository
{
    private const string SelectColumns = """
        id AS Id,
        code AS Code,
        name AS Name,
        category_type AS CategoryType,
        display_order AS DisplayOrder,
        is_active AS IsActive,
        is_deleted AS IsDeleted,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt,
        deleted_at AS DeletedAt
        """;

    public async Task<IEnumerable<MenuCategory>> GetAllAsync(string? categoryType = null, bool includeInactive = false)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM rf_menu_categories
            WHERE is_deleted = false
              AND (@IncludeInactive = true OR is_active = true)
              AND (@CategoryType IS NULL OR category_type = @CategoryType)
            ORDER BY display_order ASC, name ASC;
            """;

        return await connection.QueryAsync<MenuCategory>(sql, new
        {
            CategoryType = categoryType,
            IncludeInactive = includeInactive
        });
    }

    public async Task<MenuCategory?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM rf_menu_categories
            WHERE id = @Id AND is_deleted = false;
            """;

        return await connection.QuerySingleOrDefaultAsync<MenuCategory>(sql, new { Id = id });
    }

    public async Task<MenuCategory?> GetByCodeAsync(string code)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM rf_menu_categories
            WHERE code = @Code AND is_deleted = false;
            """;

        return await connection.QuerySingleOrDefaultAsync<MenuCategory>(sql, new { Code = code });
    }

    public async Task<bool> HasActiveMenusAsync(Guid categoryId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            SELECT EXISTS (
                SELECT 1 
                FROM menus 
                WHERE category_id = @CategoryId 
                  AND is_deleted = false
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql, new { CategoryId = categoryId });
    }

    public async Task<bool> HasAssignedMenusAsync(Guid categoryId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            SELECT EXISTS (
                SELECT 1 
                FROM menu_assignments a
                JOIN menus m ON a.menu_id = m.id
                WHERE m.category_id = @CategoryId 
                  AND m.is_deleted = false
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql, new { CategoryId = categoryId });
    }

    public async Task<Guid> CreateAsync(MenuCategory category)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            INSERT INTO rf_menu_categories (
                code,
                name,
                category_type,
                display_order,
                is_active,
                is_deleted,
                created_at,
                updated_at
            )
            VALUES (
                @Code,
                @Name,
                @CategoryType,
                @DisplayOrder,
                @IsActive,
                false,
                NOW(),
                NOW()
            )
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<Guid>(sql, category);
    }

    public async Task UpdateAsync(MenuCategory category)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            UPDATE rf_menu_categories
            SET name = @Name,
                category_type = @CategoryType,
                display_order = @DisplayOrder,
                is_active = @IsActive,
                updated_at = NOW()
            WHERE id = @Id AND is_deleted = false;
            """;

        await connection.ExecuteAsync(sql, category);
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            UPDATE rf_menu_categories
            SET is_deleted = true,
                is_active = false,
                deleted_at = NOW(),
                updated_at = NOW()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id });
    }
}
