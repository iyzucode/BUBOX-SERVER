using Bubox.Domain.Constants;
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
                updated_at AS UpdatedAt,
                avatar_updated_at AS AvatarUpdatedAt,
                avatar_changed_at AS AvatarChangedAt,
                full_name_changed_at AS FullNameChangedAt,
                username_changed_at AS UsernameChangedAt,
                phone_number AS PhoneNumber,
                whatsapp_number AS WhatsappNumber
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
                updated_at AS UpdatedAt,
                avatar_updated_at AS AvatarUpdatedAt,
                avatar_changed_at AS AvatarChangedAt,
                full_name_changed_at AS FullNameChangedAt,
                username_changed_at AS UsernameChangedAt,
                phone_number AS PhoneNumber,
                whatsapp_number AS WhatsappNumber
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
                updated_at AS UpdatedAt,
                avatar_updated_at AS AvatarUpdatedAt,
                avatar_changed_at AS AvatarChangedAt,
                full_name_changed_at AS FullNameChangedAt,
                username_changed_at AS UsernameChangedAt,
                phone_number AS PhoneNumber,
                whatsapp_number AS WhatsappNumber
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
                updated_at AS UpdatedAt,
                avatar_updated_at AS AvatarUpdatedAt,
                avatar_changed_at AS AvatarChangedAt,
                full_name_changed_at AS FullNameChangedAt,
                username_changed_at AS UsernameChangedAt,
                phone_number AS PhoneNumber,
                whatsapp_number AS WhatsappNumber
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

    public async Task UpdatePasswordAsync(Guid userId, string? passwordHash)
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

    private const string InsertUserSql = """
        INSERT INTO users (id, username, email, password_hash, full_name, is_email_verified, created_at, updated_at)
        VALUES (@Id, @Username, @Email, @PasswordHash, @FullName, @IsEmailVerified, @CreatedAt, @UpdatedAt);
        """;

    private const string InsertRoleSql = """
        INSERT INTO user_roles (user_id, role_id)
        SELECT @UserId, r.id
        FROM rf_roles r
        WHERE UPPER(r.code) = UPPER(@RoleCode)
        ON CONFLICT (user_id, role_id) DO NOTHING;
        """;

    private const string InsertExternalLoginSql = """
        INSERT INTO user_external_logins (id, user_id, provider, provider_user_id, email, created_at, last_login_at)
        VALUES (@Id, @UserId, @Provider, @ProviderUserId, @Email, @CreatedAt, @LastLoginAt);
        """;

    public async Task<User?> GetByExternalLoginAsync(string provider, string providerUserId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                u.id AS Id,
                u.username AS Username,
                u.email AS Email,
                u.password_hash AS PasswordHash,
                u.full_name AS FullName,
                u.is_email_verified AS IsEmailVerified,
                u.created_at AS CreatedAt,
                u.updated_at AS UpdatedAt,
                u.avatar_updated_at AS AvatarUpdatedAt,
                u.avatar_changed_at AS AvatarChangedAt,
                u.full_name_changed_at AS FullNameChangedAt,
                u.username_changed_at AS UsernameChangedAt,
                u.phone_number AS PhoneNumber,
                u.whatsapp_number AS WhatsappNumber
            FROM user_external_logins el
            JOIN users u ON u.id = el.user_id
            WHERE el.provider = @Provider AND el.provider_user_id = @ProviderUserId;
            """;

        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Provider = provider, ProviderUserId = providerUserId });
    }

    public async Task AddExternalLoginAsync(UserExternalLogin login)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(InsertExternalLoginSql, login);
    }

    public async Task TouchExternalLoginAsync(string provider, string providerUserId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE user_external_logins
            SET last_login_at = @Now
            WHERE provider = @Provider AND provider_user_id = @ProviderUserId;
            """;

        await connection.ExecuteAsync(sql, new { Provider = provider, ProviderUserId = providerUserId, Now = DateTime.UtcNow });
    }

    public async Task CreateWithExternalLoginAsync(User user, string roleCode, UserExternalLogin login)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(InsertUserSql, user, transaction);
        await connection.ExecuteAsync(InsertRoleSql, new { UserId = user.Id, RoleCode = roleCode }, transaction);
        await connection.ExecuteAsync(InsertExternalLoginSql, login, transaction);

        transaction.Commit();
    }

    public async Task<bool> TryUpsertAvatarAsync(UserAvatar avatar, DateTime changeAllowedBefore)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // Klaim jatah perubahan dulu (atomik); bila masih dalam batas waktu, tidak ada yang disimpan.
        const string claimSql = """
            UPDATE users
            SET avatar_updated_at = @UpdatedAt, avatar_changed_at = @UpdatedAt, updated_at = @UpdatedAt
            WHERE id = @UserId AND (avatar_changed_at IS NULL OR avatar_changed_at <= @ChangeAllowedBefore);
            """;
        const string upsertSql = """
            INSERT INTO user_avatars (user_id, content_type, data, updated_at)
            VALUES (@UserId, @ContentType, @Data, @UpdatedAt)
            ON CONFLICT (user_id) DO UPDATE
            SET content_type = EXCLUDED.content_type, data = EXCLUDED.data, updated_at = EXCLUDED.updated_at;
            """;

        var claimed = await connection.ExecuteAsync(
            claimSql,
            new { avatar.UserId, avatar.UpdatedAt, ChangeAllowedBefore = changeAllowedBefore },
            transaction);
        if (claimed == 0)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(upsertSql, avatar, transaction);
        transaction.Commit();
        return true;
    }

    public async Task<bool> TryUpdateFullNameAsync(Guid userId, string fullName, DateTime changeAllowedBefore)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE users
            SET full_name = @FullName, full_name_changed_at = @Now, updated_at = @Now
            WHERE id = @UserId AND (full_name_changed_at IS NULL OR full_name_changed_at <= @ChangeAllowedBefore);
            """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            FullName = fullName,
            Now = DateTime.UtcNow,
            ChangeAllowedBefore = changeAllowedBefore,
        });
        return affected > 0;
    }

    public async Task<UsernameUpdateResult> TryUpdateUsernameAsync(Guid userId, string username, DateTime changeAllowedBefore)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE users
            SET username = @Username, username_changed_at = @Now, updated_at = @Now
            WHERE id = @UserId AND (username_changed_at IS NULL OR username_changed_at <= @ChangeAllowedBefore);
            """;

        try
        {
            var affected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                Username = username,
                Now = DateTime.UtcNow,
                ChangeAllowedBefore = changeAllowedBefore,
            });
            return affected > 0 ? UsernameUpdateResult.Updated : UsernameUpdateResult.CooldownActive;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            // Username diambil akun lain di sela pengecekan (indeks unik lower(username)).
            return UsernameUpdateResult.Taken;
        }
    }

    public async Task UpdateContactAsync(Guid userId, string? phoneNumber, string? whatsappNumber)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE users
            SET phone_number = @PhoneNumber, whatsapp_number = @WhatsappNumber, updated_at = @Now
            WHERE id = @UserId;
            """;

        await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            PhoneNumber = phoneNumber,
            WhatsappNumber = whatsappNumber,
            Now = DateTime.UtcNow,
        });
    }

    public async Task DeleteAvatarAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            "DELETE FROM user_avatars WHERE user_id = @UserId;",
            new { UserId = userId },
            transaction);
        await connection.ExecuteAsync(
            "UPDATE users SET avatar_updated_at = NULL, updated_at = @Now WHERE id = @UserId;",
            new { UserId = userId, Now = DateTime.UtcNow },
            transaction);
        transaction.Commit();
    }

    public async Task<UserAvatar?> GetAvatarAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT user_id AS UserId, content_type AS ContentType, data AS Data, updated_at AS UpdatedAt
            FROM user_avatars
            WHERE user_id = @UserId;
            """;

        return await connection.QuerySingleOrDefaultAsync<UserAvatar>(sql, new { UserId = userId });
    }
}
