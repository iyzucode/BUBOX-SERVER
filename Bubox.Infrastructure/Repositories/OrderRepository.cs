using System.Text;
using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class OrderRepository(IDbConnectionFactory connectionFactory) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OrderSql.OrderColumns}
            FROM orders o
            WHERE o.id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<Order>(sql, new { Id = id });
    }

    public async Task<OrderPaymentStatus?> GetPaymentStatusAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                o.id AS OrderId,
                o.user_id AS UserId,
                o.status_code AS StatusCode,
                o.payment_due_at AS PaymentDueAt,
                o.paid_at AS PaidAt,
                o.cancel_reason AS CancelReason
            FROM orders o
            WHERE o.id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<OrderPaymentStatus>(sql, new { Id = id });
    }

    public async Task<IEnumerable<OrderListItem>> GetByUserIdAsync(Guid userId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OrderSql.OrderListColumns}, 0 AS TotalCount
            FROM orders o
            JOIN users u ON u.id = o.user_id
            {OrderSql.FirstItemJoin}
            WHERE o.user_id = @UserId
            ORDER BY o.created_at DESC;
            """;

        return await connection.QueryAsync<OrderListItem>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<OrderListItem>> SearchAsync(OrderSearchFilter filter)
    {
        using var connection = connectionFactory.CreateConnection();

        var where = new StringBuilder("WHERE 1 = 1");
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.StatusCode))
        {
            where.Append(" AND o.status_code = @StatusCode");
            parameters.Add("StatusCode", filter.StatusCode);
        }

        if (filter.DeliveryDate.HasValue)
        {
            where.Append(" AND EXISTS (SELECT 1 FROM order_days dd WHERE dd.order_id = o.id AND dd.delivery_date = @DeliveryDate::date)");
            parameters.Add("DeliveryDate", filter.DeliveryDate.Value.Date);
        }

        if (filter.CreatedFromUtc.HasValue)
        {
            where.Append(" AND o.created_at >= @CreatedFrom");
            parameters.Add("CreatedFrom", filter.CreatedFromUtc.Value);
        }

        if (filter.CreatedToUtc.HasValue)
        {
            where.Append(" AND o.created_at < @CreatedTo");
            parameters.Add("CreatedTo", filter.CreatedToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            where.Append(" AND (o.order_number ILIKE @Search OR u.full_name ILIKE @Search OR u.email ILIKE @Search)");
            parameters.Add("Search", $"%{filter.Search.Trim()}%");
        }

        parameters.Add("Limit", filter.PageSize);
        parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);

        var sql = $"""
            SELECT {OrderSql.OrderListColumns}, COUNT(*) OVER()::int AS TotalCount
            FROM orders o
            JOIN users u ON u.id = o.user_id
            {OrderSql.FirstItemJoin}
            {where}
            ORDER BY o.created_at DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        return await connection.QueryAsync<OrderListItem>(sql, parameters);
    }

    public async Task<IEnumerable<OrderDay>> GetDaysByOrderIdAsync(Guid orderId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OrderSql.DayColumns}
            FROM order_days d
            WHERE d.order_id = @OrderId
            ORDER BY d.delivery_date ASC;
            """;

        return await connection.QueryAsync<OrderDay>(sql, new { OrderId = orderId });
    }

    public async Task<OrderDay?> GetDayByIdAsync(Guid dayId)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OrderSql.DayColumns}
            FROM order_days d
            WHERE d.id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<OrderDay>(sql, new { Id = dayId });
    }

    public async Task<IEnumerable<OrderItem>> GetItemsByDayIdsAsync(IReadOnlyCollection<Guid> dayIds)
    {
        if (dayIds.Count == 0)
        {
            return [];
        }

        using var connection = connectionFactory.CreateConnection();
        var sql = $"""
            SELECT {OrderSql.ItemColumns},
                   CASE WHEN m.image_url IS NOT NULL AND m.image_url <> '' THEN m.updated_at END AS MenuImageUpdatedAt
            FROM order_items i
            LEFT JOIN menus m ON m.id = i.menu_id
            WHERE i.order_day_id = ANY(@DayIds)
            ORDER BY i.category_name ASC, i.menu_name ASC;
            """;

        return await connection.QueryAsync<OrderItem>(sql, new { DayIds = dayIds.ToArray() });
    }

    public async Task<IEnumerable<AdminDayRow>> GetAdminDayRowsAsync(AdminDayRowQuery query)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            WITH matched_orders AS (
                SELECT DISTINCT o.id
                FROM orders o
                JOIN users u ON u.id = o.user_id
                JOIN order_days d ON d.order_id = o.id
                WHERE (@OrderId::uuid IS NULL OR o.id = @OrderId::uuid)
                  AND (@DeliveryFrom::date IS NULL OR d.delivery_date >= @DeliveryFrom::date)
                  AND (@DeliveryTo::date IS NULL OR d.delivery_date <= @DeliveryTo::date)
                  AND (@CreatedFrom::timestamptz IS NULL OR o.created_at >= @CreatedFrom::timestamptz)
                  AND (@CreatedTo::timestamptz IS NULL OR o.created_at < @CreatedTo::timestamptz)
                  AND (@Search::text IS NULL
                       OR o.order_number ILIKE @Search::text
                       OR u.full_name ILIKE @Search::text
                       OR u.email ILIKE @Search::text
                       OR COALESCE(u.phone_number, '') ILIKE @Search::text
                       OR COALESCE(o.address_snapshot->>'phoneNumber', '') ILIKE @Search::text
                       OR COALESCE(o.address_snapshot->>'recipientName', '') ILIKE @Search::text)
            ),
            days AS (
                SELECT d.*,
                       ROW_NUMBER() OVER (PARTITION BY d.order_id ORDER BY d.delivery_date) AS day_index,
                       COUNT(*) OVER (PARTITION BY d.order_id) AS day_count
                FROM order_days d
                JOIN matched_orders m ON m.id = d.order_id
            )
            SELECT
                d.id AS OrderDayId,
                o.id AS OrderId,
                o.order_number AS OrderNumber,
                u.full_name AS CustomerName,
                u.email AS CustomerEmail,
                u.phone_number AS CustomerPhone,
                d.delivery_date AS DeliveryDate,
                d.day_index::int AS DayIndex,
                d.day_count::int AS DayCount,
                d.status_code AS DayStatus,
                d.day_subtotal AS DaySubtotal,
                d.requires_manual_refund AS RequiresManualRefund,
                d.updated_at AS DayUpdatedAt,
                d.cancelled_at AS DayCancelledAt,
                o.status_code AS OrderStatus,
                o.fulfillment_type AS FulfillmentType,
                o.address_snapshot::text AS AddressSnapshot,
                o.outlet_id AS OutletId,
                o.outlet_snapshot::text AS OutletSnapshot,
                o.notes AS Notes,
                o.created_at AS OrderCreatedAt,
                o.payment_due_at AS PaymentDueAt,
                o.paid_at AS PaidAt,
                o.grand_total AS GrandTotal,
                a.zone_id AS ZoneId,
                a.zone_name AS ZoneName,
                a.driver_user_id AS DriverUserId,
                du.full_name AS DriverName,
                a.assignment_source AS AssignmentSource,
                COALESCE(p.needs_review, FALSE) AS PaymentNeedsReview,
                p.review_note AS PaymentReviewNote,
                d.refunded_at AS RefundedAt,
                ru.full_name AS RefundedByName,
                d.refund_note AS RefundNote,
                o.subtotal AS Subtotal,
                o.discount_amount AS DiscountAmount,
                o.voucher_code AS VoucherCode,
                o.delivery_fee AS DeliveryFee,
                o.service_fee AS ServiceFee,
                o.tax_amount AS TaxAmount
            FROM days d
            JOIN orders o ON o.id = d.order_id
            JOIN users u ON u.id = o.user_id
            LEFT JOIN v_order_day_delivery_assignments a ON a.order_day_id = d.id
            LEFT JOIN users du ON du.id = a.driver_user_id
            LEFT JOIN users ru ON ru.id = d.refunded_by
            LEFT JOIN LATERAL (
                SELECT pay.needs_review, pay.review_note
                FROM payments pay
                WHERE pay.order_id = o.id
                ORDER BY pay.created_at DESC
                LIMIT 1
            ) p ON TRUE
            WHERE @WholeOrders
               OR ((@DeliveryFrom::date IS NULL OR d.delivery_date >= @DeliveryFrom::date)
                   AND (@DeliveryTo::date IS NULL OR d.delivery_date <= @DeliveryTo::date))
            ORDER BY d.delivery_date ASC, o.order_number ASC;
            """;

        return await connection.QueryAsync<AdminDayRow>(sql, new
        {
            query.OrderId,
            DeliveryFrom = query.DeliveryFrom?.Date,
            DeliveryTo = query.DeliveryTo?.Date,
            CreatedFrom = query.CreatedFromUtc,
            CreatedTo = query.CreatedToUtc,
            Search = query.SearchPattern,
            query.WholeOrders
        });
    }

    public async Task<OrderActivity> GetActivitySinceAsync(DateTime sinceUtc)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                COUNT(*) FILTER (WHERE created_at > @Since)::int AS NewOrders,
                COUNT(*) FILTER (WHERE paid_at > @Since)::int AS NewlyPaid,
                MAX(GREATEST(created_at, COALESCE(paid_at, created_at))) AS LatestAt
            FROM orders
            WHERE created_at > @Since OR paid_at > @Since;
            """;
        return await connection.QuerySingleAsync<OrderActivity>(sql, new { Since = sinceUtc });
    }

    public async Task<IReadOnlyList<Guid>> MarkDaysRefundedAsync(IReadOnlyCollection<Guid> dayIds, string? note, Guid adminId, DateTime nowUtc)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE order_days
            SET refunded_at = @Now, refunded_by = @AdminId, refund_note = @Note
            WHERE id = ANY(@Ids) AND requires_manual_refund AND refunded_at IS NULL
            RETURNING id;
            """;
        var ids = await connection.QueryAsync<Guid>(sql, new { Ids = dayIds.ToArray(), Note = note, AdminId = adminId, Now = nowUtc });
        return ids.ToList();
    }

    public async Task<IEnumerable<OrderStatusHistory>> GetHistoriesAsync(Guid orderId)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                id AS Id,
                order_id AS OrderId,
                scope AS Scope,
                entity_id AS EntityId,
                from_code AS FromCode,
                to_code AS ToCode,
                actor_user_id AS ActorUserId,
                actor_role AS ActorRole,
                reason AS Reason,
                created_at AS CreatedAt
            FROM order_status_histories
            WHERE order_id = @OrderId
            ORDER BY created_at ASC;
            """;

        return await connection.QueryAsync<OrderStatusHistory>(sql, new { OrderId = orderId });
    }

    public async Task<IEnumerable<OrderDayScheduleRow>> GetDaysByDeliveryDateAsync(
        DateTime deliveryDate, IReadOnlyCollection<string> statusCodes, string? fulfillmentType)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                d.id AS OrderDayId,
                o.id AS OrderId,
                o.order_number AS OrderNumber,
                u.full_name AS CustomerName,
                d.delivery_date AS DeliveryDate,
                d.status_code AS StatusCode,
                o.fulfillment_type AS FulfillmentType,
                o.address_snapshot::text AS AddressSnapshot,
                o.outlet_snapshot::text AS OutletSnapshot,
                o.notes AS Notes,
                a.zone_id AS ZoneId,
                a.zone_name AS ZoneName,
                a.driver_user_id AS DriverUserId,
                du.full_name AS DriverName,
                a.assignment_source AS AssignmentSource
            FROM order_days d
            JOIN orders o ON o.id = d.order_id
            JOIN users u ON u.id = o.user_id
            LEFT JOIN v_order_day_delivery_assignments a ON a.order_day_id = d.id
            LEFT JOIN users du ON du.id = a.driver_user_id
            WHERE d.delivery_date = @DeliveryDate::date
              AND d.status_code = ANY(@StatusCodes)
              AND (@FulfillmentType::text IS NULL OR o.fulfillment_type = @FulfillmentType::text)
            ORDER BY o.fulfillment_type ASC, o.order_number ASC;
            """;

        return await connection.QueryAsync<OrderDayScheduleRow>(sql, new
        {
            DeliveryDate = deliveryDate.Date,
            StatusCodes = statusCodes.ToArray(),
            FulfillmentType = fulfillmentType
        });
    }

    public async Task<IEnumerable<Guid>> GetOverdueAwaitingPaymentIdsAsync(DateTime dueBeforeUtc, int limit)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT id
            FROM orders
            WHERE status_code = 'AWAITING_PAYMENT'
              AND payment_due_at < @DueBefore
            ORDER BY payment_due_at ASC
            LIMIT @Limit;
            """;

        return await connection.QueryAsync<Guid>(sql, new { DueBefore = dueBeforeUtc, Limit = limit });
    }
}
