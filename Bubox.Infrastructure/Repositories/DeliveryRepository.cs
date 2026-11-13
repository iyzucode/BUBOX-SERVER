using System.Data;
using System.Data.Common;
using Dapper;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class DeliveryRepository(IDbConnectionFactory connectionFactory) : IDeliveryRepository
{
    /// <summary>Kode wilayah & semua induknya: "34.71.01.1001" → ["34", "34.71", "34.71.01", "34.71.01.1001"].</summary>
    private static string[] SelfAndAncestors(string code)
    {
        var lengths = new[] { 2, 5, 8, 13 };
        return lengths.Where(l => l <= code.Length).Select(l => code[..l]).Distinct().ToArray();
    }

    /// <summary>Panjang kode anak langsung dari sebuah induk (provinsi → kab/kota → kecamatan → kelurahan).</summary>
    private static int ChildLength(string? parentCode) => parentCode?.Length switch
    {
        null or 0 => 2,
        2 => 5,
        5 => 8,
        8 => 13,
        _ => -1
    };

    // ==========================================
    // Master wilayah
    // ==========================================

    public async Task<WilayahNode?> GetWilayahAsync(string code)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = "SELECT kode AS Code, nama AS Name FROM rf_wilayah WHERE kode = @Code;";
        return await connection.QuerySingleOrDefaultAsync<WilayahNode>(sql, new { Code = code });
    }

    public async Task<IEnumerable<WilayahNode>> GetChildrenAsync(string? parentCode)
    {
        var length = ChildLength(parentCode);
        if (length < 0) return [];

        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT kode AS Code, nama AS Name
            FROM rf_wilayah
            WHERE LENGTH(kode) = @Length AND (@Prefix::text IS NULL OR kode LIKE @Prefix)
            ORDER BY nama;
            """;
        return await connection.QueryAsync<WilayahNode>(sql, new
        {
            Length = length,
            Prefix = string.IsNullOrEmpty(parentCode) ? null : parentCode + ".%"
        });
    }

    public async Task<IEnumerable<WilayahNode>> SearchWilayahAsync(string query, int limit)
    {
        using var connection = connectionFactory.CreateConnection();
        // Kab/kota & kecamatan didahulukan (paling sering dipakai untuk aturan), lalu yang namanya diawali kata kunci
        const string sql = """
            SELECT kode AS Code, nama AS Name
            FROM rf_wilayah
            WHERE nama ILIKE @Like
            ORDER BY (nama ILIKE @Prefix) DESC,
                     CASE LENGTH(kode) WHEN 5 THEN 0 WHEN 8 THEN 1 WHEN 2 THEN 2 ELSE 3 END,
                     nama
            LIMIT @Limit;
            """;
        return await connection.QueryAsync<WilayahNode>(sql, new { Like = $"%{query}%", Prefix = $"{query}%", Limit = limit });
    }

    public async Task<IEnumerable<WilayahNode>> SearchByPostalCodeAsync(string postalCode, int limit)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT w.kode AS Code, w.nama AS Name, k.kodepos AS PostalCode
            FROM rf_kodepos k
            JOIN rf_wilayah w ON w.kode = k.kode
            WHERE k.kodepos = @PostalCode
            ORDER BY w.kode
            LIMIT @Limit;
            """;
        return await connection.QueryAsync<WilayahNode>(sql, new { PostalCode = postalCode, Limit = limit });
    }

    public async Task<IEnumerable<WilayahNode>> GetWilayahByCodesAsync(IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return [];
        using var connection = connectionFactory.CreateConnection();
        const string sql = "SELECT kode AS Code, nama AS Name FROM rf_wilayah WHERE kode = ANY(@Codes);";
        return await connection.QueryAsync<WilayahNode>(sql, new { Codes = codes.ToArray() });
    }

    // ==========================================
    // Aturan
    // ==========================================

    public async Task<CoverageRule?> GetEffectiveCoverageAsync(string wilayahCode)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT wilayah_code AS WilayahCode, is_covered AS IsCovered
            FROM delivery_coverage_areas
            WHERE wilayah_code = ANY(@Codes)
            ORDER BY LENGTH(wilayah_code) DESC
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<CoverageRule>(sql, new { Codes = SelfAndAncestors(wilayahCode) });
    }

    public async Task<IEnumerable<CoverageRule>> GetCoverageRulesAsync(IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return [];
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT wilayah_code AS WilayahCode, is_covered AS IsCovered
            FROM delivery_coverage_areas
            WHERE wilayah_code = ANY(@Codes);
            """;
        return await connection.QueryAsync<CoverageRule>(sql, new { Codes = codes.ToArray() });
    }

    public async Task<IEnumerable<ZoneAreaRule>> GetZoneAreaRulesAsync(IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return [];
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT za.wilayah_code AS WilayahCode, za.zone_id AS ZoneId, z.name AS ZoneName, z.is_active AS ZoneIsActive
            FROM delivery_zone_areas za
            JOIN delivery_zones z ON z.id = za.zone_id
            WHERE za.wilayah_code = ANY(@Codes);
            """;
        return await connection.QueryAsync<ZoneAreaRule>(sql, new { Codes = codes.ToArray() });
    }

    public async Task<IEnumerable<DescendantRuleCount>> CountDescendantRulesAsync(IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return [];
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT c.code AS Code,
                   (SELECT COUNT(*)::int FROM delivery_coverage_areas ca WHERE ca.wilayah_code LIKE c.code || '.%') AS CoverageRules,
                   (SELECT COUNT(*)::int FROM delivery_zone_areas za WHERE za.wilayah_code LIKE c.code || '.%') AS ZoneRules
            FROM UNNEST(@Codes::text[]) AS c(code);
            """;
        return await connection.QueryAsync<DescendantRuleCount>(sql, new { Codes = codes.ToArray() });
    }

    public async Task SetCoverageAsync(string wilayahCode, bool? isCovered, Guid updatedBy, DateTime nowUtc)
    {
        using var connection = connectionFactory.CreateConnection();
        if (isCovered is null)
        {
            await connection.ExecuteAsync("DELETE FROM delivery_coverage_areas WHERE wilayah_code = @Code;", new { Code = wilayahCode });
            return;
        }

        const string sql = """
            INSERT INTO delivery_coverage_areas (wilayah_code, is_covered, updated_by, updated_at)
            VALUES (@Code, @IsCovered, @By, @Now)
            ON CONFLICT (wilayah_code) DO UPDATE
            SET is_covered = EXCLUDED.is_covered, updated_by = EXCLUDED.updated_by, updated_at = EXCLUDED.updated_at;
            """;
        await connection.ExecuteAsync(sql, new { Code = wilayahCode, IsCovered = isCovered.Value, By = updatedBy, Now = nowUtc });
    }

    public async Task SetZoneAreaAsync(string wilayahCode, Guid? zoneId, Guid updatedBy, DateTime nowUtc)
    {
        using var connection = connectionFactory.CreateConnection();
        if (zoneId is null)
        {
            await connection.ExecuteAsync("DELETE FROM delivery_zone_areas WHERE wilayah_code = @Code;", new { Code = wilayahCode });
            return;
        }

        const string sql = """
            INSERT INTO delivery_zone_areas (wilayah_code, zone_id, updated_by, updated_at)
            VALUES (@Code, @ZoneId, @By, @Now)
            ON CONFLICT (wilayah_code) DO UPDATE
            SET zone_id = EXCLUDED.zone_id, updated_by = EXCLUDED.updated_by, updated_at = EXCLUDED.updated_at;
            """;
        await connection.ExecuteAsync(sql, new { Code = wilayahCode, ZoneId = zoneId.Value, By = updatedBy, Now = nowUtc });
    }

    // ==========================================
    // Zona
    // ==========================================

    private const string ZoneSelect = """
        SELECT z.id AS Id, z.code AS Code, z.name AS Name, z.description AS Description, z.is_active AS IsActive,
               z.created_at AS CreatedAt, z.updated_at AS UpdatedAt,
               (SELECT COUNT(*)::int FROM delivery_zone_areas za WHERE za.zone_id = z.id) AS AreaCount,
               zd.driver_user_id AS DriverUserId, u.full_name AS DriverName, u.email AS DriverEmail
        FROM delivery_zones z
        LEFT JOIN delivery_zone_drivers zd ON zd.zone_id = z.id AND zd.unassigned_at IS NULL
        LEFT JOIN users u ON u.id = zd.driver_user_id
        """;

    public async Task<IEnumerable<string>> GetZoneAreaCodesAsync(Guid zoneId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = "SELECT wilayah_code FROM delivery_zone_areas WHERE zone_id = @ZoneId ORDER BY wilayah_code;";
        return await connection.QueryAsync<string>(sql, new { ZoneId = zoneId });
    }

    public async Task<bool> DeleteZoneIfEmptyAsync(Guid zoneId)
    {
        using var connection = connectionFactory.CreateConnection();
        // Syarat kosong dicek di statement yang sama agar tidak kebobolan pemetaan/driver yang baru ditambah
        const string sql = """
            DELETE FROM delivery_zones z
            WHERE z.id = @Id
              AND NOT EXISTS (SELECT 1 FROM delivery_zone_areas za WHERE za.zone_id = z.id)
              AND NOT EXISTS (SELECT 1 FROM delivery_zone_drivers zd WHERE zd.zone_id = z.id AND zd.unassigned_at IS NULL);
            """;
        return await connection.ExecuteAsync(sql, new { Id = zoneId }) > 0;
    }

    public async Task<IEnumerable<DeliveryZone>> GetZonesAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<DeliveryZone>($"{ZoneSelect} ORDER BY z.is_active DESC, z.name;");
    }

    public async Task<DeliveryZone?> GetZoneAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DeliveryZone>($"{ZoneSelect} WHERE z.id = @Id;", new { Id = id });
    }

    public async Task<bool> ZoneCodeExistsAsync(string code, Guid? excludeId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT EXISTS (SELECT 1 FROM delivery_zones WHERE code = @Code AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId));
            """;
        return await connection.ExecuteScalarAsync<bool>(sql, new { Code = code, ExcludeId = excludeId });
    }

    public async Task CreateZoneAsync(DeliveryZone zone, Guid createdBy)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO delivery_zones (id, code, name, description, is_active, created_by, updated_by, created_at, updated_at)
            VALUES (@Id, @Code, @Name, @Description, @IsActive, @By, @By, @CreatedAt, @UpdatedAt);
            """;
        await connection.ExecuteAsync(sql, new
        {
            zone.Id, zone.Code, zone.Name, zone.Description, zone.IsActive, By = createdBy, zone.CreatedAt, zone.UpdatedAt
        });
    }

    public async Task UpdateZoneAsync(DeliveryZone zone, Guid updatedBy)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE delivery_zones
            SET code = @Code, name = @Name, description = @Description, is_active = @IsActive,
                updated_by = @By, updated_at = @UpdatedAt
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(sql, new { zone.Id, zone.Code, zone.Name, zone.Description, zone.IsActive, By = updatedBy, zone.UpdatedAt });
    }

    public async Task SetZoneDriverAsync(Guid zoneId, Guid? driverUserId, Guid changedBy, DateTime nowUtc)
    {
        await using var scope = await BeginAsync();
        var (connection, transaction) = scope;

        // Kunci zona agar dua admin yang mengganti driver bersamaan tidak membuat dua driver aktif
        await connection.ExecuteAsync("SELECT 1 FROM delivery_zones WHERE id = @Id FOR UPDATE;", new { Id = zoneId }, transaction);

        var current = await connection.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT driver_user_id FROM delivery_zone_drivers WHERE zone_id = @ZoneId AND unassigned_at IS NULL;",
            new { ZoneId = zoneId }, transaction);

        if (current == driverUserId)
        {
            await scope.CommitAsync();
            return;
        }

        await connection.ExecuteAsync("""
            UPDATE delivery_zone_drivers SET unassigned_at = @Now, unassigned_by = @By
            WHERE zone_id = @ZoneId AND unassigned_at IS NULL;
            """, new { ZoneId = zoneId, Now = nowUtc, By = changedBy }, transaction);

        if (driverUserId is { } driver)
        {
            await connection.ExecuteAsync("""
                INSERT INTO delivery_zone_drivers (id, zone_id, driver_user_id, assigned_at, assigned_by)
                VALUES (@Id, @ZoneId, @DriverId, @Now, @By);
                """, new { Id = Guid.NewGuid(), ZoneId = zoneId, DriverId = driver, Now = nowUtc, By = changedBy }, transaction);
        }

        await scope.CommitAsync();
    }

    public async Task<IEnumerable<ZoneDriverHistoryRow>> GetZoneDriverHistoryAsync(Guid zoneId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT zd.driver_user_id AS DriverUserId, u.full_name AS DriverName,
                   zd.assigned_at AS AssignedAt, zd.unassigned_at AS UnassignedAt
            FROM delivery_zone_drivers zd
            JOIN users u ON u.id = zd.driver_user_id
            WHERE zd.zone_id = @ZoneId
            ORDER BY zd.assigned_at DESC
            LIMIT 50;
            """;
        return await connection.QueryAsync<ZoneDriverHistoryRow>(sql, new { ZoneId = zoneId });
    }

    // ==========================================
    // Driver
    // ==========================================

    public async Task<IEnumerable<DriverRow>> GetDriversAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT u.id AS UserId, u.full_name AS FullName, u.email AS Email, u.username AS Username,
                   u.phone_number AS PhoneNumber,
                   (SELECT STRING_AGG(z.name, ', ' ORDER BY z.name)
                    FROM delivery_zone_drivers zd JOIN delivery_zones z ON z.id = zd.zone_id
                    WHERE zd.driver_user_id = u.id AND zd.unassigned_at IS NULL) AS ZoneNames
            FROM users u
            JOIN user_roles ur ON ur.user_id = u.id
            JOIN rf_roles r ON r.id = ur.role_id AND r.code = @Driver
            ORDER BY u.full_name;
            """;
        return await connection.QueryAsync<DriverRow>(sql, new { Driver = RoleConstants.Codes.Driver });
    }

    public async Task<bool> IsDriverAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM user_roles ur JOIN rf_roles r ON r.id = ur.role_id
                WHERE ur.user_id = @UserId AND r.code = @Driver);
            """;
        return await connection.ExecuteScalarAsync<bool>(sql, new { UserId = userId, Driver = RoleConstants.Codes.Driver });
    }

    public async Task<IEnumerable<UserSearchRow>> SearchUsersAsync(string query, int limit)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT u.id AS UserId, u.full_name AS FullName, u.email AS Email, u.username AS Username,
                   EXISTS (SELECT 1 FROM user_roles ur JOIN rf_roles r ON r.id = ur.role_id
                           WHERE ur.user_id = u.id AND r.code = @Driver) AS IsDriver
            FROM users u
            WHERE u.full_name ILIKE @Like OR u.email ILIKE @Like OR u.username ILIKE @Like
            ORDER BY u.full_name
            LIMIT @Limit;
            """;
        return await connection.QueryAsync<UserSearchRow>(sql, new { Like = $"%{query}%", Limit = limit, Driver = RoleConstants.Codes.Driver });
    }

    public async Task GrantDriverRoleAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO user_roles (user_id, role_id)
            SELECT @UserId, r.id FROM rf_roles r WHERE r.code = @Driver
            ON CONFLICT DO NOTHING;
            """;
        await connection.ExecuteAsync(sql, new { UserId = userId, Driver = RoleConstants.Codes.Driver });
    }

    public async Task RevokeDriverRoleAsync(Guid userId, Guid changedBy, DateTime nowUtc)
    {
        await using var scope = await BeginAsync();
        var (connection, transaction) = scope;

        await connection.ExecuteAsync("""
            DELETE FROM user_roles ur USING rf_roles r
            WHERE ur.role_id = r.id AND r.code = @Driver AND ur.user_id = @UserId;
            """, new { UserId = userId, Driver = RoleConstants.Codes.Driver }, transaction);

        await connection.ExecuteAsync("""
            UPDATE delivery_zone_drivers SET unassigned_at = @Now, unassigned_by = @By
            WHERE driver_user_id = @UserId AND unassigned_at IS NULL;
            """, new { UserId = userId, Now = nowUtc, By = changedBy }, transaction);

        // Kiriman yang belum berangkat kembali mengikuti zona
        await connection.ExecuteAsync("""
            UPDATE order_days
            SET driver_override_user_id = NULL, override_reason = NULL, override_by = @By, override_at = @Now
            WHERE driver_override_user_id = @UserId AND dispatched_at IS NULL;
            """, new { UserId = userId, Now = nowUtc, By = changedBy }, transaction);

        await scope.CommitAsync();
    }

    // ==========================================
    // Penugasan per hari kirim
    // ==========================================

    public async Task<IEnumerable<DayAssignment>> GetDayAssignmentsAsync(IReadOnlyCollection<Guid> orderDayIds)
    {
        if (orderDayIds.Count == 0) return [];
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT a.order_day_id AS OrderDayId, a.zone_id AS ZoneId, a.zone_name AS ZoneName,
                   a.driver_user_id AS DriverUserId, u.full_name AS DriverName, a.assignment_source AS AssignmentSource
            FROM v_order_day_delivery_assignments a
            LEFT JOIN users u ON u.id = a.driver_user_id
            WHERE a.order_day_id = ANY(@Ids);
            """;
        return await connection.QueryAsync<DayAssignment>(sql, new { Ids = orderDayIds.ToArray() });
    }

    public async Task<int> SetDayDriverOverrideAsync(
        IReadOnlyCollection<Guid> orderDayIds, Guid? driverUserId, string? reason, Guid changedBy, DateTime nowUtc)
    {
        if (orderDayIds.Count == 0) return 0;
        using var connection = connectionFactory.CreateConnection();
        // Hanya hari kirim pesanan DELIVERY yang belum berangkat (yang sudah berangkat sudah dibekukan)
        const string sql = """
            UPDATE order_days d
            SET driver_override_user_id = @DriverId,
                override_reason = @Reason,
                override_by = @By,
                override_at = @Now
            FROM orders o
            WHERE o.id = d.order_id
              AND o.fulfillment_type = @Delivery
              AND d.id = ANY(@Ids)
              AND d.dispatched_at IS NULL;
            """;
        return await connection.ExecuteAsync(sql, new
        {
            Ids = orderDayIds.ToArray(),
            DriverId = driverUserId,
            Reason = reason,
            By = changedBy,
            Now = nowUtc,
            Delivery = FulfillmentTypes.Delivery
        });
    }

    // ==========================================
    // Transaksi sederhana
    // ==========================================

    private async Task<DbScope> BeginAsync()
    {
        var connection = (DbConnection)connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        return new DbScope(connection, transaction);
    }

    private sealed class DbScope(DbConnection connection, DbTransaction transaction) : IAsyncDisposable
    {
        private bool _committed;

        public void Deconstruct(out IDbConnection conn, out IDbTransaction tx)
        {
            conn = connection;
            tx = transaction;
        }

        public async Task CommitAsync()
        {
            await transaction.CommitAsync();
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed) await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
