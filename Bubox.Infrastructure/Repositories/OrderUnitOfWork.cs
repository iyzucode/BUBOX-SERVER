using System.Data;
using System.Data.Common;
using Dapper;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class OrderUnitOfWorkFactory(IDbConnectionFactory connectionFactory) : IOrderUnitOfWorkFactory
{
    public async Task<IOrderUnitOfWork> BeginAsync()
    {
        var connection = connectionFactory.CreateConnection();
        try
        {
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync();
                var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                return new OrderUnitOfWork(dbConnection, transaction);
            }

            connection.Open();
            return new OrderUnitOfWork(connection, connection.BeginTransaction(IsolationLevel.ReadCommitted));
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}

/// <summary>
/// Satu transaksi database untuk perubahan order. Jika tidak di-commit, transaksi di-rollback saat dispose.
/// </summary>
public sealed class OrderUnitOfWork(IDbConnection connection, IDbTransaction transaction) : IOrderUnitOfWork
{
    private bool _completed;

    public async Task<Order?> LockOrderAsync(Guid orderId)
    {
        var sql = $"""
            SELECT {OrderSql.OrderColumns}
            FROM orders o
            WHERE o.id = @Id
            FOR UPDATE;
            """;

        return await connection.QuerySingleOrDefaultAsync<Order>(sql, new { Id = orderId }, transaction);
    }

    public async Task<List<OrderDay>> GetDaysAsync(Guid orderId)
    {
        var sql = $"""
            SELECT {OrderSql.DayColumns}
            FROM order_days d
            WHERE d.order_id = @OrderId
            ORDER BY d.delivery_date ASC;
            """;

        var days = await connection.QueryAsync<OrderDay>(sql, new { OrderId = orderId }, transaction);
        return days.ToList();
    }

    public async Task<Payment?> GetPaymentAsync(Guid orderId)
    {
        var sql = $"""
            SELECT {OrderSql.PaymentColumns}
            FROM payments p
            WHERE p.order_id = @OrderId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Payment>(sql, new { OrderId = orderId }, transaction);
    }

    public async Task InsertOrderAsync(Order order)
    {
        const string sql = """
            INSERT INTO orders (
                id, order_number, user_id, status_code, fulfillment_type, address_id, address_snapshot,
                subtotal, discount_amount, delivery_fee, grand_total, notes, payment_due_at, created_at, updated_at)
            VALUES (
                @Id, @OrderNumber, @UserId, @StatusCode, @FulfillmentType, @AddressId, @AddressSnapshot::jsonb,
                @Subtotal, @DiscountAmount, @DeliveryFee, @GrandTotal, @Notes, @PaymentDueAt, @CreatedAt, @UpdatedAt);
            """;

        await connection.ExecuteAsync(sql, order, transaction);
    }

    public async Task InsertDaysAsync(IEnumerable<OrderDay> days)
    {
        const string sql = """
            INSERT INTO order_days (id, order_id, delivery_date, day_of_week, status_code, day_subtotal, created_at, updated_at)
            VALUES (@Id, @OrderId, @DeliveryDate::date, @DayOfWeek, @StatusCode, @DaySubtotal, @CreatedAt, @UpdatedAt);
            """;

        await connection.ExecuteAsync(sql, days.Select(d => new
        {
            d.Id,
            d.OrderId,
            DeliveryDate = d.DeliveryDate.Date,
            d.DayOfWeek,
            d.StatusCode,
            d.DaySubtotal,
            d.CreatedAt,
            d.UpdatedAt
        }), transaction);
    }

    public async Task InsertItemsAsync(IEnumerable<OrderItem> items)
    {
        const string sql = """
            INSERT INTO order_items (
                id, order_day_id, menu_id, menu_name, category_id, category_code, category_name,
                unit_price, qty, line_total, created_at)
            VALUES (
                @Id, @OrderDayId, @MenuId, @MenuName, @CategoryId, @CategoryCode, @CategoryName,
                @UnitPrice, @Qty, @LineTotal, @CreatedAt);
            """;

        await connection.ExecuteAsync(sql, items, transaction);
    }

    public async Task InsertHistoryAsync(OrderStatusHistory history)
    {
        const string sql = """
            INSERT INTO order_status_histories (
                id, order_id, scope, entity_id, from_code, to_code, actor_user_id, actor_role, reason, created_at)
            VALUES (
                @Id, @OrderId, @Scope, @EntityId, @FromCode, @ToCode, @ActorUserId, @ActorRole, @Reason, @CreatedAt);
            """;

        await connection.ExecuteAsync(sql, history, transaction);
    }

    public async Task<bool> UpdateOrderStatusAsync(
        Guid orderId, string fromCode, string toCode, DateTime nowUtc, string? cancelReason, Guid? cancelledBy)
    {
        const string sql = """
            UPDATE orders
            SET status_code = @To,
                updated_at = @Now,
                paid_at = CASE WHEN @IsPaid THEN @Now ELSE paid_at END,
                completed_at = CASE WHEN @IsCompleted THEN @Now ELSE completed_at END,
                cancelled_at = CASE WHEN @IsCancel THEN @Now ELSE cancelled_at END,
                cancel_reason = CASE WHEN @IsCancel THEN @CancelReason ELSE cancel_reason END,
                cancelled_by = CASE WHEN @IsCancel THEN @CancelledBy ELSE cancelled_by END
            WHERE id = @Id AND status_code = @From;
            """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            Id = orderId,
            From = fromCode,
            To = toCode,
            Now = nowUtc,
            IsPaid = toCode == OrderStatusCodes.Paid,
            IsCompleted = toCode == OrderStatusCodes.Completed,
            IsCancel = OrderStatusCodes.Cancelled.Contains(toCode),
            CancelReason = cancelReason,
            CancelledBy = cancelledBy
        }, transaction);

        return affected == 1;
    }

    public async Task<bool> UpdateDayStatusAsync(
        Guid dayId, string fromCode, string toCode, DateTime nowUtc, string? cancelReason, Guid? cancelledBy, bool requiresManualRefund)
    {
        const string sql = """
            UPDATE order_days
            SET status_code = @To,
                updated_at = @Now,
                requires_manual_refund = requires_manual_refund OR @RequiresManualRefund,
                cancelled_at = CASE WHEN @IsCancel THEN @Now ELSE cancelled_at END,
                cancel_reason = CASE WHEN @IsCancel THEN @CancelReason ELSE cancel_reason END,
                cancelled_by = CASE WHEN @IsCancel THEN @CancelledBy ELSE cancelled_by END
            WHERE id = @Id AND status_code = @From;
            """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            Id = dayId,
            From = fromCode,
            To = toCode,
            Now = nowUtc,
            RequiresManualRefund = requiresManualRefund,
            IsCancel = toCode is OrderDayStatusCodes.Cancelled or OrderDayStatusCodes.CancelledByAdmin,
            CancelReason = cancelReason,
            CancelledBy = cancelledBy
        }, transaction);

        return affected == 1;
    }

    public async Task UpdatePaymentAsync(Payment payment)
    {
        const string sql = """
            UPDATE payments
            SET transaction_id = @TransactionId,
                transaction_status = @TransactionStatus,
                acquirer = @Acquirer,
                paid_at = @PaidAt,
                needs_review = @NeedsReview,
                review_note = @ReviewNote,
                updated_at = @UpdatedAt
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, payment, transaction);
    }

    public Task CommitAsync()
    {
        _completed = true;
        if (transaction is DbTransaction dbTransaction)
        {
            return dbTransaction.CommitAsync();
        }

        transaction.Commit();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_completed)
            {
                if (transaction is DbTransaction dbTransaction)
                {
                    await dbTransaction.RollbackAsync();
                }
                else
                {
                    transaction.Rollback();
                }
            }
        }
        finally
        {
            transaction.Dispose();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.DisposeAsync();
            }
            else
            {
                connection.Dispose();
            }
        }
    }
}
