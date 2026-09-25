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

    public async Task SaveOtpAsync(UserOtp otp)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO user_otps (id, user_id, otp_code, type, expires_at, is_used, created_at)
            VALUES (@Id, @UserId, @OtpCode, @Type, @ExpiresAt, @IsUsed, @CreatedAt);
            """;

        await connection.ExecuteAsync(sql, otp);
    }

    public async Task<UserOtp?> GetValidOtpAsync(Guid userId, string otpCode, string type)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                id AS Id,
                user_id AS UserId,
                otp_code AS OtpCode,
                type AS Type,
                expires_at AS ExpiresAt,
                is_used AS IsUsed,
                created_at AS CreatedAt
            FROM user_otps
            WHERE user_id = @UserId 
              AND otp_code = @OtpCode 
              AND type = @Type 
              AND is_used = FALSE 
              AND expires_at > @Now
            ORDER BY created_at DESC
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<UserOtp>(sql, new
        {
            UserId = userId,
            OtpCode = otpCode,
            Type = type,
            Now = DateTime.UtcNow
        });
    }

    public async Task MarkOtpUsedAsync(Guid otpId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE user_otps
            SET is_used = TRUE
            WHERE id = @OtpId;
            """;

        await connection.ExecuteAsync(sql, new { OtpId = otpId });
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
