using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class PaymentRepository(IDbConnectionFactory connectionFactory) : IPaymentRepository
{
    public async Task<Payment?> GetByOrderIdAsync(Guid orderId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OrderSql.PaymentColumns}
            FROM payments p
            WHERE p.order_id = @OrderId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Payment>(sql, new { OrderId = orderId });
    }

    public async Task<Payment?> GetByProviderOrderIdAsync(string providerOrderId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OrderSql.PaymentColumns}
            FROM payments p
            WHERE p.provider_order_id = @ProviderOrderId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Payment>(sql, new { ProviderOrderId = providerOrderId });
    }

    public async Task CreateAsync(Payment payment)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO payments (
                id, order_id, provider, provider_order_id, payment_type, transaction_id, transaction_status,
                acquirer, qr_string, qr_url, gross_amount, expires_at, paid_at, needs_review, review_note, created_at, updated_at)
            VALUES (
                @Id, @OrderId, @Provider, @ProviderOrderId, @PaymentType, @TransactionId, @TransactionStatus,
                @Acquirer, @QrString, @QrUrl, @GrossAmount, @ExpiresAt, @PaidAt, @NeedsReview, @ReviewNote, @CreatedAt, @UpdatedAt);
            """;

        await connection.ExecuteAsync(sql, payment);
    }

    public async Task SaveNotificationAsync(PaymentNotification notification)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO payment_notifications (
                id, payment_id, provider_order_id, transaction_status, signature_valid, raw_payload, received_at)
            VALUES (
                @Id, @PaymentId, @ProviderOrderId, @TransactionStatus, @SignatureValid, @RawPayload::jsonb, @ReceivedAt);
            """;

        await connection.ExecuteAsync(sql, notification);
    }
}

public class OrderStatusTransitionRepository(IDbConnectionFactory connectionFactory) : IOrderStatusTransitionRepository
{
    public async Task<IEnumerable<OrderStatusTransition>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                id AS Id,
                scope AS Scope,
                from_code AS FromCode,
                to_code AS ToCode,
                allowed_roles AS AllowedRoles
            FROM rf_order_status_transitions;
            """;

        return await connection.QueryAsync<OrderStatusTransition>(sql);
    }
}

public class AppSettingRepository(IDbConnectionFactory connectionFactory) : IAppSettingRepository
{
    public async Task<IEnumerable<AppSetting>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                id AS Id,
                key AS Key,
                value AS Value,
                value_type AS ValueType,
                description AS Description,
                updated_by AS UpdatedBy,
                updated_at AS UpdatedAt
            FROM app_settings
            ORDER BY key;
            """;

        return await connection.QueryAsync<AppSetting>(sql);
    }

    public async Task UpsertManyAsync(IReadOnlyCollection<AppSetting> settings, Guid updatedBy)
    {
        using var connection = connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = """
                INSERT INTO app_settings (key, value, value_type, description, updated_by, updated_at)
                VALUES (@Key, @Value, @ValueType, @Description, @UpdatedBy, @UpdatedAt)
                ON CONFLICT (key) DO UPDATE
                SET value = EXCLUDED.value,
                    updated_by = EXCLUDED.updated_by,
                    updated_at = EXCLUDED.updated_at;
                """;

            var now = DateTime.UtcNow;
            await connection.ExecuteAsync(sql, settings.Select(s => new
            {
                s.Key,
                s.Value,
                s.ValueType,
                s.Description,
                UpdatedBy = updatedBy,
                UpdatedAt = now
            }), transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
