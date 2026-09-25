using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class UserAddressRepository(IDbConnectionFactory connectionFactory) : IUserAddressRepository
{
    private const string SelectColumns = """
        id AS Id,
        user_id AS UserId,
        label AS Label,
        recipient_name AS RecipientName,
        phone_number AS PhoneNumber,
        full_address AS FullAddress,
        subdistrict AS Subdistrict,
        city AS City,
        province AS Province,
        postal_code AS PostalCode,
        notes AS Notes,
        is_primary AS IsPrimary,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt
        """;

    public async Task<IEnumerable<UserAddress>> GetByUserIdAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM user_addresses
            WHERE user_id = @UserId
            ORDER BY is_primary DESC, created_at DESC;
            """;

        return await connection.QueryAsync<UserAddress>(sql, new { UserId = userId });
    }

    public async Task<UserAddress?> GetByIdAsync(Guid id, Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM user_addresses
            WHERE id = @Id AND user_id = @UserId;
            """;

        return await connection.QuerySingleOrDefaultAsync<UserAddress>(sql, new { Id = id, UserId = userId });
    }

    public async Task<UserAddress?> GetPrimaryAddressAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {SelectColumns}
            FROM user_addresses
            WHERE user_id = @UserId AND is_primary = TRUE
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<UserAddress>(sql, new { UserId = userId });
    }

    public async Task<Guid> CreateAsync(UserAddress address)
    {
        using var connection = connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            if (address.IsPrimary)
            {
                const string resetPrimarySql = """
                    UPDATE user_addresses
                    SET is_primary = FALSE, updated_at = @UpdatedAt
                    WHERE user_id = @UserId AND is_primary = TRUE;
                    """;
                await connection.ExecuteAsync(resetPrimarySql, new { address.UserId, UpdatedAt = DateTime.UtcNow }, transaction);
            }

            const string insertSql = """
                INSERT INTO user_addresses (
                    id, user_id, label, recipient_name, phone_number,
                    full_address, subdistrict, city, province, postal_code,
                    notes, is_primary, created_at, updated_at
                )
                VALUES (
                    @Id, @UserId, @Label, @RecipientName, @PhoneNumber,
                    @FullAddress, @Subdistrict, @City, @Province, @PostalCode,
                    @Notes, @IsPrimary, @CreatedAt, @UpdatedAt
                );
                """;

            await connection.ExecuteAsync(insertSql, address, transaction);
            transaction.Commit();
            return address.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateAsync(UserAddress address)
    {
        using var connection = connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            if (address.IsPrimary)
            {
                const string resetPrimarySql = """
                    UPDATE user_addresses
                    SET is_primary = FALSE, updated_at = @UpdatedAt
                    WHERE user_id = @UserId AND id <> @Id AND is_primary = TRUE;
                    """;
                await connection.ExecuteAsync(resetPrimarySql, new { address.UserId, address.Id, UpdatedAt = DateTime.UtcNow }, transaction);
            }

            const string updateSql = """
                UPDATE user_addresses
                SET 
                    label = @Label,
                    recipient_name = @RecipientName,
                    phone_number = @PhoneNumber,
                    full_address = @FullAddress,
                    subdistrict = @Subdistrict,
                    city = @City,
                    province = @Province,
                    postal_code = @PostalCode,
                    notes = @Notes,
                    is_primary = @IsPrimary,
                    updated_at = @UpdatedAt
                WHERE id = @Id AND user_id = @UserId;
                """;

            await connection.ExecuteAsync(updateSql, address, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task SetPrimaryAddressAsync(Guid addressId, Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            const string resetPrimarySql = """
                UPDATE user_addresses
                SET is_primary = FALSE, updated_at = @UpdatedAt
                WHERE user_id = @UserId AND is_primary = TRUE;
                """;
            await connection.ExecuteAsync(resetPrimarySql, new { UserId = userId, UpdatedAt = DateTime.UtcNow }, transaction);

            const string setPrimarySql = """
                UPDATE user_addresses
                SET is_primary = TRUE, updated_at = @UpdatedAt
                WHERE id = @AddressId AND user_id = @UserId;
                """;
            await connection.ExecuteAsync(setPrimarySql, new { AddressId = addressId, UserId = userId, UpdatedAt = DateTime.UtcNow }, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteAsync(Guid addressId, Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            DELETE FROM user_addresses
            WHERE id = @AddressId AND user_id = @UserId;
            """;

        await connection.ExecuteAsync(sql, new { AddressId = addressId, UserId = userId });
    }

    public async Task<int> CountByUserIdAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT COUNT(1)
            FROM user_addresses
            WHERE user_id = @UserId;
            """;

        return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
    }
}
