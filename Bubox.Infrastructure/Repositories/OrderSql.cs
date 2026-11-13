namespace Bubox.Infrastructure.Repositories;

/// <summary>Potongan SQL kolom yang dipakai bersama oleh repository order.</summary>
internal static class OrderSql
{
    public const string OrderColumns = """
        o.id AS Id,
        o.order_number AS OrderNumber,
        o.user_id AS UserId,
        o.status_code AS StatusCode,
        o.fulfillment_type AS FulfillmentType,
        o.address_id AS AddressId,
        o.address_snapshot::text AS AddressSnapshot,
        o.delivery_wilayah_code AS DeliveryWilayahCode,
        o.subtotal AS Subtotal,
        o.discount_amount AS DiscountAmount,
        o.voucher_code AS VoucherCode,
        o.delivery_fee AS DeliveryFee,
        o.grand_total AS GrandTotal,
        o.notes AS Notes,
        o.payment_due_at AS PaymentDueAt,
        o.paid_at AS PaidAt,
        o.completed_at AS CompletedAt,
        o.cancelled_at AS CancelledAt,
        o.cancel_reason AS CancelReason,
        o.cancelled_by AS CancelledBy,
        o.created_at AS CreatedAt,
        o.updated_at AS UpdatedAt
        """;

    public const string DayColumns = """
        d.id AS Id,
        d.order_id AS OrderId,
        d.delivery_date AS DeliveryDate,
        d.day_of_week AS DayOfWeek,
        d.status_code AS StatusCode,
        d.day_subtotal AS DaySubtotal,
        d.requires_manual_refund AS RequiresManualRefund,
        d.cancel_reason AS CancelReason,
        d.cancelled_by AS CancelledBy,
        d.cancelled_at AS CancelledAt,
        d.created_at AS CreatedAt,
        d.updated_at AS UpdatedAt
        """;

    public const string ItemColumns = """
        i.id AS Id,
        i.order_day_id AS OrderDayId,
        i.menu_id AS MenuId,
        i.menu_name AS MenuName,
        i.category_id AS CategoryId,
        i.category_code AS CategoryCode,
        i.category_name AS CategoryName,
        i.unit_price AS UnitPrice,
        i.qty AS Qty,
        i.line_total AS LineTotal,
        i.created_at AS CreatedAt
        """;

    public const string PaymentColumns = """
        p.id AS Id,
        p.order_id AS OrderId,
        p.provider AS Provider,
        p.provider_order_id AS ProviderOrderId,
        p.payment_type AS PaymentType,
        p.transaction_id AS TransactionId,
        p.transaction_status AS TransactionStatus,
        p.acquirer AS Acquirer,
        p.qr_string AS QrString,
        p.qr_url AS QrUrl,
        p.gross_amount AS GrossAmount,
        p.expires_at AS ExpiresAt,
        p.paid_at AS PaidAt,
        p.needs_review AS NeedsReview,
        p.review_note AS ReviewNote,
        p.created_at AS CreatedAt,
        p.updated_at AS UpdatedAt
        """;

    public const string OrderListColumns = """
        o.id AS Id,
        o.order_number AS OrderNumber,
        o.user_id AS UserId,
        u.full_name AS CustomerName,
        u.email AS CustomerEmail,
        o.status_code AS StatusCode,
        o.fulfillment_type AS FulfillmentType,
        o.grand_total AS GrandTotal,
        o.payment_due_at AS PaymentDueAt,
        o.created_at AS CreatedAt,
        (SELECT COUNT(*)::int FROM order_days dd WHERE dd.order_id = o.id) AS DaysCount,
        (SELECT MIN(dd.delivery_date) FROM order_days dd WHERE dd.order_id = o.id) AS FirstDeliveryDate,
        (SELECT MAX(dd.delivery_date) FROM order_days dd WHERE dd.order_id = o.id) AS LastDeliveryDate,
        (SELECT array_agg(to_char(dd.delivery_date, 'YYYY-MM-DD') ORDER BY dd.delivery_date)
            FROM order_days dd WHERE dd.order_id = o.id) AS DeliveryDates,
        (SELECT COUNT(*)::int FROM order_items ii JOIN order_days dd ON dd.id = ii.order_day_id
            WHERE dd.order_id = o.id) AS ItemLinesCount,
        first_item.menu_id AS FirstMenuId,
        first_item.menu_name AS FirstMenuName
        """;

    /// <summary>Menu pertama (hari kirim paling awal) untuk thumbnail di daftar order. Dipakai bersama OrderListColumns.</summary>
    public const string FirstItemJoin = """
        LEFT JOIN LATERAL (
            SELECT ii.menu_id, ii.menu_name
            FROM order_items ii
            JOIN order_days dd ON dd.id = ii.order_day_id
            WHERE dd.order_id = o.id
            ORDER BY dd.delivery_date ASC, ii.created_at ASC, ii.menu_name ASC
            LIMIT 1
        ) first_item ON TRUE
        """;
}
