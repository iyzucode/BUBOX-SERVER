using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class OutletRepository(IDbConnectionFactory connectionFactory) : IOutletRepository
{
    private const string SelectFrom = """
        SELECT
            o.id AS Id,
            o.name AS Name,
            o.full_address AS FullAddress,
            o.wilayah_code AS WilayahCode,
            o.pickup_start_time AS PickupStartTime,
            o.pickup_end_time AS PickupEndTime,
            o.whatsapp_number AS WhatsappNumber,
            o.latitude AS Latitude,
            o.longitude AS Longitude,
            o.sort_order AS SortOrder,
            o.is_active AS IsActive,
            o.created_by AS CreatedBy,
            o.updated_by AS UpdatedBy,
            o.created_at AS CreatedAt,
            o.updated_at AS UpdatedAt,
            CONCAT_WS(', ', kel.nama, kec.nama, kab.nama, prov.nama) AS WilayahPath,
            (SELECT COUNT(*)::int FROM orders ord WHERE ord.outlet_id = o.id) AS OrderCount
        FROM outlets o
        LEFT JOIN rf_wilayah kel ON kel.kode = o.wilayah_code
        LEFT JOIN rf_wilayah kec ON kec.kode = LEFT(o.wilayah_code, 8)
        LEFT JOIN rf_wilayah kab ON kab.kode = LEFT(o.wilayah_code, 5)
        LEFT JOIN rf_wilayah prov ON prov.kode = LEFT(o.wilayah_code, 2)
        """;

    public async Task<IEnumerable<Outlet>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<Outlet>($"{SelectFrom} ORDER BY o.sort_order, o.name;");
    }

    public async Task<IEnumerable<Outlet>> GetActiveAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<Outlet>($"{SelectFrom} WHERE o.is_active ORDER BY o.sort_order, o.name;");
    }

    public async Task<Outlet?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Outlet>($"{SelectFrom} WHERE o.id = @Id;", new { Id = id });
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM outlets
                WHERE LOWER(name) = LOWER(@Name) AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId::uuid));
            """;
        return await connection.ExecuteScalarAsync<bool>(sql, new { Name = name, ExcludeId = excludeId });
    }

    public async Task<Guid?> GetLastUsedOutletIdAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT outlet_id FROM orders
            WHERE user_id = @UserId AND outlet_id IS NOT NULL
            ORDER BY created_at DESC
            LIMIT 1;
            """;
        return await connection.ExecuteScalarAsync<Guid?>(sql, new { UserId = userId });
    }

    public async Task<int> GetNextSortOrderAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COALESCE(MAX(sort_order) + 1, 0) FROM outlets;");
    }

    public async Task CreateAsync(Outlet outlet)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO outlets (
                id, name, full_address, wilayah_code, pickup_start_time, pickup_end_time, whatsapp_number, latitude, longitude,
                sort_order, is_active, created_by, updated_by, created_at, updated_at)
            VALUES (
                @Id, @Name, @FullAddress, @WilayahCode, @PickupStartTime, @PickupEndTime, @WhatsappNumber, @Latitude, @Longitude,
                @SortOrder, @IsActive, @CreatedBy, @UpdatedBy, @CreatedAt, @UpdatedAt);
            """;
        await connection.ExecuteAsync(sql, outlet);
    }

    public async Task UpdateAsync(Outlet outlet)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE outlets SET
                name = @Name,
                full_address = @FullAddress,
                wilayah_code = @WilayahCode,
                pickup_start_time = @PickupStartTime,
                pickup_end_time = @PickupEndTime,
                whatsapp_number = @WhatsappNumber,
                latitude = @Latitude,
                longitude = @Longitude,
                is_active = @IsActive,
                updated_by = @UpdatedBy,
                updated_at = @UpdatedAt
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(sql, outlet);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, Guid updatedBy, DateTime nowUtc)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE outlets SET is_active = @IsActive, updated_by = @UpdatedBy, updated_at = @NowUtc
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive, UpdatedBy = updatedBy, NowUtc = nowUtc });
    }

    public async Task<bool> DeleteIfUnusedAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            DELETE FROM outlets o
            WHERE o.id = @Id
              AND NOT EXISTS (SELECT 1 FROM orders ord WHERE ord.outlet_id = o.id);
            """;
        return await connection.ExecuteAsync(sql, new { Id = id }) > 0;
    }
}
