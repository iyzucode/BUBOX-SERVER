using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class VoucherRepository(IDbConnectionFactory connectionFactory) : IVoucherRepository
{
    private const string SelectColumns = """
        v.id AS Id,
        v.code AS Code,
        v.title AS Title,
        v.description AS Description,
        v.discount_type AS DiscountType,
        v.discount_value AS DiscountValue,
        v.max_discount AS MaxDiscount,
        v.min_subtotal AS MinSubtotal,
        v.payment_methods::text[] AS PaymentMethods,
        v.usage_limit AS UsageLimit,
        v.starts_at AS StartsAt,
        v.ends_at AS EndsAt,
        v.is_listed AS IsListed,
        v.is_active AS IsActive,
        v.created_by AS CreatedBy,
        v.updated_by AS UpdatedBy,
        v.created_at AS CreatedAt,
        v.updated_at AS UpdatedAt,
        (SELECT COUNT(*)::int FROM voucher_redemptions r
         WHERE r.voucher_id = v.id AND r.released_at IS NULL) AS RedemptionCount
        """;

    public async Task<IEnumerable<Voucher>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM vouchers v
            ORDER BY v.created_at DESC;
            """;

        return await connection.QueryAsync<Voucher>(sql);
    }

    public async Task<Voucher?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM vouchers v
            WHERE v.id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<Voucher>(sql, new { Id = id });
    }

    public async Task<Voucher?> GetByCodeAsync(string code)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM vouchers v
            WHERE v.code = @Code;
            """;

        return await connection.QuerySingleOrDefaultAsync<Voucher>(sql, new { Code = code });
    }

    public async Task<IEnumerable<Voucher>> GetListedActiveAsync(DateTime nowUtc)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM vouchers v
            WHERE v.is_active AND v.is_listed
              AND v.starts_at <= @Now AND v.ends_at > @Now
            ORDER BY v.ends_at ASC, v.created_at DESC;
            """;

        return await connection.QueryAsync<Voucher>(sql, new { Now = nowUtc });
    }

    public async Task<IReadOnlySet<Guid>> GetUsedVoucherIdsAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT voucher_id
            FROM voucher_redemptions
            WHERE user_id = @UserId AND released_at IS NULL;
            """;

        var ids = await connection.QueryAsync<Guid>(sql, new { UserId = userId });
        return ids.ToHashSet();
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM vouchers
                WHERE code = @Code AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId)
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql, new { Code = code, ExcludeId = excludeId });
    }

    public async Task CreateAsync(Voucher voucher)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO vouchers (
                id, code, title, description, discount_type, discount_value, max_discount, min_subtotal,
                payment_methods, usage_limit, starts_at, ends_at, is_listed, is_active, created_by, updated_by,
                created_at, updated_at)
            VALUES (
                @Id, @Code, @Title, @Description, @DiscountType, @DiscountValue, @MaxDiscount, @MinSubtotal,
                @PaymentMethods::varchar(20)[], @UsageLimit, @StartsAt, @EndsAt, @IsListed, @IsActive, @CreatedBy,
                @UpdatedBy, @CreatedAt, @UpdatedAt);
            """;

        await connection.ExecuteAsync(sql, voucher);
    }

    public async Task UpdateAsync(Voucher voucher)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE vouchers
            SET code = @Code,
                title = @Title,
                description = @Description,
                discount_type = @DiscountType,
                discount_value = @DiscountValue,
                max_discount = @MaxDiscount,
                min_subtotal = @MinSubtotal,
                payment_methods = @PaymentMethods::varchar(20)[],
                usage_limit = @UsageLimit,
                starts_at = @StartsAt,
                ends_at = @EndsAt,
                is_listed = @IsListed,
                is_active = @IsActive,
                updated_by = @UpdatedBy,
                updated_at = @UpdatedAt
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, voucher);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, Guid updatedBy, DateTime nowUtc)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE vouchers
            SET is_active = @IsActive, updated_by = @UpdatedBy, updated_at = @Now
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive, UpdatedBy = updatedBy, Now = nowUtc });
    }
}
