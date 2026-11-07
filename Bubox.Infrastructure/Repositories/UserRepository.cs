using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                id AS Id,
                username AS Username,
                email AS Email,
                password_hash AS PasswordHash,
                full_name AS FullName,
                is_email_verified AS IsEmailVerified,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM users
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                id AS Id,
                username AS Username,
                email AS Email,
                password_hash AS PasswordHash,
                full_name AS FullName,
                is_email_verified AS IsEmailVerified,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM users
            WHERE LOWER(email) = LOWER(@Email);
            """;

        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                id AS Id,
                username AS Username,
                email AS Email,
                password_hash AS PasswordHash,
                full_name AS FullName,
                is_email_verified AS IsEmailVerified,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM users
            WHERE LOWER(username) = LOWER(@Username);
            """;

        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<User?> GetByIdentifierAsync(string identifier)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                id AS Id,
                username AS Username,
                email AS Email,
                password_hash AS PasswordHash,
                full_name AS FullName,
                is_email_verified AS IsEmailVerified,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM users
            WHERE LOWER(email) = LOWER(@Identifier) OR LOWER(username) = LOWER(@Identifier);
            """;

        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Identifier = identifier });
    }

    public async Task<Guid> CreateAsync(User user)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO users (id, username, email, password_hash, full_name, is_email_verified, created_at, updated_at)
            VALUES (@Id, @Username, @Email, @PasswordHash, @FullName, @IsEmailVerified, @CreatedAt, @UpdatedAt);
            """;

        await connection.ExecuteAsync(sql, user);
        return user.Id;
    }

    public async Task UpdatePasswordAsync(Guid userId, string passwordHash)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE users
            SET password_hash = @PasswordHash, updated_at = @UpdatedAt
            WHERE id = @UserId;
            """;

        await connection.ExecuteAsync(sql, new { UserId = userId, PasswordHash = passwordHash, UpdatedAt = DateTime.UtcNow });
    }

    public async Task SetEmailVerifiedAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE users
            SET is_email_verified = TRUE, updated_at = @UpdatedAt
            WHERE id = @UserId;
            """;

        await connection.ExecuteAsync(sql, new { UserId = userId, UpdatedAt = DateTime.UtcNow });
    }

    private const string OtpColumns = """
        id AS Id,
        user_id AS UserId,
        otp_hash AS OtpHash,
        type AS Type,
        expires_at AS ExpiresAt,
        is_used AS IsUsed,
        attempt_count AS AttemptCount,
        created_at AS CreatedAt
        """;

    public async Task ReplaceActiveOtpAsync(UserOtp otp)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        const string invalidateSql = """
            UPDATE user_otps
            SET is_used = TRUE
            WHERE user_id = @UserId AND type = @Type AND is_used = FALSE;
            """;
        const string insertSql = """
            INSERT INTO user_otps (id, user_id, otp_hash, type, expires_at, is_used, attempt_count, created_at)
            VALUES (@Id, @UserId, @OtpHash, @Type, @ExpiresAt, FALSE, 0, @CreatedAt);
            """;

        await connection.ExecuteAsync(invalidateSql, new { otp.UserId, otp.Type }, transaction);
        await connection.ExecuteAsync(insertSql, otp, transaction);
        transaction.Commit();
    }

    public async Task<UserOtp?> GetActiveOtpAsync(Guid userId, string type)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OtpColumns}
            FROM user_otps
            WHERE user_id = @UserId
              AND type = @Type
              AND is_used = FALSE
              AND expires_at > @Now
            ORDER BY created_at DESC
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<UserOtp>(sql, new
        {
            UserId = userId,
            Type = type,
            Now = DateTime.UtcNow
        });
    }

    public async Task<int> IncrementOtpAttemptAsync(Guid otpId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE user_otps
            SET attempt_count = attempt_count + 1
            WHERE id = @OtpId
            RETURNING attempt_count;
            """;

        return await connection.ExecuteScalarAsync<int>(sql, new { OtpId = otpId });
    }

    public async Task<bool> TryMarkOtpUsedAsync(Guid otpId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE user_otps
            SET is_used = TRUE
            WHERE id = @OtpId AND is_used = FALSE;
            """;

        return await connection.ExecuteAsync(sql, new { OtpId = otpId }) == 1;
    }

    public async Task<OtpSendStats> GetOtpSendStatsAsync(Guid userId, string type, DateTime since)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                MAX(created_at) AS LastCreatedAt,
                COUNT(*) FILTER (WHERE created_at >= @Since)::int AS CountInWindow
            FROM user_otps
            WHERE user_id = @UserId AND type = @Type;
            """;

        return await connection.QuerySingleAsync<OtpSendStats>(sql, new { UserId = userId, Type = type, Since = since });
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT r.name
            FROM user_roles ur
            JOIN rf_roles r ON r.id = ur.role_id
            WHERE ur.user_id = @UserId
            ORDER BY r.name;
            """;

        return await connection.QueryAsync<string>(sql, new { UserId = userId });
    }

    public async Task AddUserRoleAsync(Guid userId, string roleCode)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO user_roles (user_id, role_id)
            SELECT @UserId, r.id
            FROM rf_roles r
            WHERE UPPER(r.code) = UPPER(@RoleCode)
            ON CONFLICT (user_id, role_id) DO NOTHING;
            """;

        await connection.ExecuteAsync(sql, new { UserId = userId, RoleCode = roleCode });
    }
}
