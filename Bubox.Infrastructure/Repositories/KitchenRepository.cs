using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class KitchenRepository(IDbConnectionFactory connectionFactory) : IKitchenRepository
{
    public async Task<KitchenRelease?> GetReleaseAsync(DateTime deliveryDate)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                k.delivery_date AS DeliveryDate,
                k.released_at AS ReleasedAt,
                k.released_by AS ReleasedBy,
                k.day_count AS DayCount,
                k.portions AS Portions,
                u.full_name AS ReleasedByName
            FROM kitchen_releases k
            LEFT JOIN users u ON u.id = k.released_by
            WHERE k.delivery_date = @Date::date;
            """;
        return await connection.QuerySingleOrDefaultAsync<KitchenRelease>(sql, new { Date = deliveryDate.Date });
    }

    public async Task UpsertReleaseAsync(KitchenRelease release)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO kitchen_releases (delivery_date, released_at, released_by, day_count, portions)
            VALUES (@DeliveryDate::date, @ReleasedAt, @ReleasedBy, @DayCount, @Portions)
            ON CONFLICT (delivery_date) DO UPDATE
            SET released_at = EXCLUDED.released_at,
                released_by = EXCLUDED.released_by,
                day_count = EXCLUDED.day_count,
                portions = EXCLUDED.portions;
            """;
        await connection.ExecuteAsync(sql, new
        {
            DeliveryDate = release.DeliveryDate.Date,
            release.ReleasedAt,
            release.ReleasedBy,
            release.DayCount,
            release.Portions
        });
    }

    public async Task<bool> DeleteReleaseAsync(DateTime deliveryDate)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = "DELETE FROM kitchen_releases WHERE delivery_date = @Date::date;";
        return await connection.ExecuteAsync(sql, new { Date = deliveryDate.Date }) > 0;
    }
}
