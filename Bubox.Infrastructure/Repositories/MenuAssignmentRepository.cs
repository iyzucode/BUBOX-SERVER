using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class MenuAssignmentRepository(IDbConnectionFactory connectionFactory) : IMenuAssignmentRepository
{
    private const string SelectAssignedDetail = """
        a.id AS AssignmentId,
        a.menu_id AS MenuId,
        a.day_of_week AS DayOfWeek,
        a.day_name AS DayName,
        m.category_id AS CategoryId,
        c.code AS CategoryCode,
        c.name AS CategoryName,
        c.category_type AS CategoryType,
        m.name AS Name,
        m.description AS Description,
        m.ingredients AS Ingredients,
        m.nutrition_info AS NutritionInfo,
        m.price AS Price,
        m.image_url AS ImageUrl,
        m.is_active AS IsActive,
        m.created_at AS CreatedAt,
        m.updated_at AS UpdatedAt
        """;

    public async Task<IEnumerable<AssignedMenuDetail>> GetByDayAsync(int dayOfWeek)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectAssignedDetail}
            FROM menu_assignments a
            JOIN menus m ON a.menu_id = m.id
            JOIN rf_menu_categories c ON m.category_id = c.id
            WHERE a.day_of_week = @DayOfWeek
              AND m.is_active = true
              AND m.is_deleted = false
              AND c.is_active = true
              AND c.is_deleted = false
            ORDER BY c.display_order ASC, m.name ASC;
            """;

        return await connection.QueryAsync<AssignedMenuDetail>(sql, new { DayOfWeek = dayOfWeek });
    }

    public async Task<IEnumerable<AssignedMenuDetail>> GetAllWeeklyAssignmentsAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectAssignedDetail}
            FROM menu_assignments a
            JOIN menus m ON a.menu_id = m.id
            JOIN rf_menu_categories c ON m.category_id = c.id
            WHERE m.is_active = true
              AND m.is_deleted = false
              AND c.is_active = true
              AND c.is_deleted = false
            ORDER BY a.day_of_week ASC, c.display_order ASC, m.name ASC;
            """;

        return await connection.QueryAsync<AssignedMenuDetail>(sql);
    }

    public async Task<bool> HasAssignmentsByMenuIdAsync(Guid menuId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            SELECT EXISTS (
                SELECT 1 
                FROM menu_assignments 
                WHERE menu_id = @MenuId
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql, new { MenuId = menuId });
    }

    public async Task<bool> ExistsAssignmentAsync(Guid menuId, int dayOfWeek)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            SELECT EXISTS (
                SELECT 1 
                FROM menu_assignments 
                WHERE menu_id = @MenuId AND day_of_week = @DayOfWeek
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql, new { MenuId = menuId, DayOfWeek = dayOfWeek });
    }

    public async Task<Guid> AssignMenuAsync(MenuAssignment assignment)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            INSERT INTO menu_assignments (
                id,
                menu_id,
                day_of_week,
                day_name,
                created_at
            )
            VALUES (
                @Id,
                @MenuId,
                @DayOfWeek,
                @DayName,
                NOW()
            )
            ON CONFLICT (menu_id, day_of_week) DO NOTHING
            RETURNING id;
            """;

        var insertedId = await connection.ExecuteScalarAsync<Guid?>(sql, assignment);
        return insertedId ?? assignment.Id;
    }

    public async Task<int> BatchAssignMenusAsync(IEnumerable<MenuAssignment> assignments)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = """
            INSERT INTO menu_assignments (
                id,
                menu_id,
                day_of_week,
                day_name,
                created_at
            )
            VALUES (
                @Id,
                @MenuId,
                @DayOfWeek,
                @DayName,
                NOW()
            )
            ON CONFLICT (menu_id, day_of_week) DO NOTHING;
            """;

        return await connection.ExecuteAsync(sql, assignments);
    }

    public async Task HardDeleteAsync(Guid assignmentId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = "DELETE FROM menu_assignments WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = assignmentId });
    }

    public async Task HardDeleteByMenuAndDayAsync(Guid menuId, int dayOfWeek)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = "DELETE FROM menu_assignments WHERE menu_id = @MenuId AND day_of_week = @DayOfWeek;";
        await connection.ExecuteAsync(sql, new { MenuId = menuId, DayOfWeek = dayOfWeek });
    }
}
