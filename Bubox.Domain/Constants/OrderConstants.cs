namespace Bubox.Domain.Constants;

public static class OrderStatusScopes
{
    public const string Order = "ORDER";
    public const string Day = "DAY";
}

/// <summary>Status level Order (kontrak & pembayaran).</summary>
public static class OrderStatusCodes
{
    public const string AwaitingPayment = "AWAITING_PAYMENT";
    public const string Paid = "PAID";
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string CancelledByCustomer = "CANCELLED_BY_CUSTOMER";
    public const string CancelledByAdmin = "CANCELLED_BY_ADMIN";
    public const string Expired = "EXPIRED";
    public const string PaymentFailed = "PAYMENT_FAILED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        AwaitingPayment, Paid, InProgress, Completed,
        CancelledByCustomer, CancelledByAdmin, Expired, PaymentFailed
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Completed, CancelledByCustomer, CancelledByAdmin, Expired, PaymentFailed
    };

    public static readonly IReadOnlySet<string> Cancelled = new HashSet<string>
    {
        CancelledByCustomer, CancelledByAdmin, Expired, PaymentFailed
    };

    /// <summary>Order yang pembayarannya sudah diterima.</summary>
    public static readonly IReadOnlySet<string> PaidOrLater = new HashSet<string>
    {
        Paid, InProgress, Completed
    };
}

/// <summary>Status level OrderDay (operasional per tanggal kirim).</summary>
public static class OrderDayStatusCodes
{
    public const string Scheduled = "SCHEDULED";
    public const string Confirmed = "CONFIRMED";
    public const string Preparing = "PREPARING";
    public const string Ready = "READY";
    public const string OutForDelivery = "OUT_FOR_DELIVERY";
    public const string Delivered = "DELIVERED";
    public const string PickedUp = "PICKED_UP";
    public const string Cancelled = "CANCELLED";
    public const string CancelledByAdmin = "CANCELLED_BY_ADMIN";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Scheduled, Confirmed, Preparing, Ready, OutForDelivery,
        Delivered, PickedUp, Cancelled, CancelledByAdmin
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Delivered, PickedUp, Cancelled, CancelledByAdmin
    };

    public static readonly IReadOnlySet<string> Fulfilled = new HashSet<string>
    {
        Delivered, PickedUp
    };

    /// <summary>Hari yang sudah mulai diproses dapur.</summary>
    public static readonly IReadOnlySet<string> Started = new HashSet<string>
    {
        Preparing, Ready, OutForDelivery, Delivered, PickedUp
    };

    /// <summary>Status yang boleh dibatalkan admin karena force majeure.</summary>
    public static readonly IReadOnlySet<string> AdminCancellable = new HashSet<string>
    {
        Confirmed, Preparing, Ready, OutForDelivery
    };

    /// <summary>Status maju yang boleh di-set lewat UpdateDayStatus.</summary>
    public static readonly IReadOnlySet<string> ManualProgress = new HashSet<string>
    {
        Preparing, Ready, OutForDelivery, Delivered, PickedUp
    };

    /// <summary>Hari yang masuk rekap produksi dapur (sudah dibayar dan tidak batal).</summary>
    public static readonly IReadOnlySet<string> Production = new HashSet<string>
    {
        Confirmed, Preparing, Ready, OutForDelivery, Delivered, PickedUp
    };
}

public static class FulfillmentTypes
{
    public const string Delivery = "DELIVERY";
    public const string Pickup = "PICKUP";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Delivery, Pickup };
}

public static class PaymentConstants
{
    public const string ProviderMidtrans = "MIDTRANS";
    public const string PaymentTypeQris = "qris";
}

public static class MidtransTransactionStatuses
{
    public const string Pending = "pending";
    public const string Settlement = "settlement";
    public const string Capture = "capture";
    public const string Deny = "deny";
    public const string Cancel = "cancel";
    public const string Expire = "expire";
    public const string Failure = "failure";
}

public static class AppSettingKeys
{
    public const string OrderCutoffDaysBefore = "ORDER_CUTOFF_DAYS_BEFORE";
    public const string OrderCutoffTime = "ORDER_CUTOFF_TIME";
    public const string OrderTimezone = "ORDER_TIMEZONE";
    public const string PaymentTimeoutMinutes = "PAYMENT_TIMEOUT_MINUTES";
    /// <summary>Aturan ongkir Pesan Antar (JSON, lihat FulfillmentFees).</summary>
    public const string DeliveryFeeRule = "DELIVERY_FEE_RULE";
    /// <summary>Aturan biaya Ambil Sendiri (JSON, lihat FulfillmentFees).</summary>
    public const string PickupFeeRule = "PICKUP_FEE_RULE";
    public const string ServiceFeeType = "SERVICE_FEE_TYPE";
    public const string ServiceFeeValue = "SERVICE_FEE_VALUE";
    public const string TaxPercent = "TAX_PERCENT";
}

/// <summary>Cara hitung ongkir per metode pemesanan.</summary>
public static class FulfillmentFeeModes
{
    public const string Free = "FREE";
    /// <summary>Nominal tetap per pesanan.</summary>
    public const string Fixed = "FIXED";
    /// <summary>Jumlah hari × tarif per hari bertingkat.</summary>
    public const string PerDay = "PER_DAY";
}

/// <summary>Jenis biaya layanan pesanan.</summary>
public static class ServiceFeeTypes
{
    /// <summary>Persen dari subtotal − diskon.</summary>
    public const string Percent = "PERCENT";
    /// <summary>Nominal Rupiah tetap per pesanan.</summary>
    public const string Fixed = "FIXED";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Percent, Fixed };
}

public static class AppSettingValueTypes
{
    public const string Int = "INT";
    public const string Decimal = "DECIMAL";
    public const string Time = "TIME";
    public const string String = "STRING";
    public const string Bool = "BOOL";
}

public static class OrderActorRoles
{
    /// <summary>Pelaku untuk transisi otomatis (webhook, job, cascade).</summary>
    public const string System = "SYSTEM";
}

public static class OrderLimits
{
    public const int MaxDaysPerOrder = 31;
    public const int MaxItemsPerDay = 30;
    public const int MinQtyPerItem = 1;
    public const int MaxQtyPerItem = 20;
    public const int MaxAdvanceDays = 60;
}
