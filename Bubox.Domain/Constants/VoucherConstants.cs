namespace Bubox.Domain.Constants;

public static class VoucherDiscountTypes
{
    /// <summary>Potongan persen dari subtotal, opsional dibatasi MaxDiscount.</summary>
    public const string Percent = "PERCENT";

    /// <summary>Potongan nominal Rupiah.</summary>
    public const string Fixed = "FIXED";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Percent, Fixed };
}

/// <summary>Metode pembayaran yang bisa dipilih untuk voucher. Saat ini pesanan hanya memakai QRIS.</summary>
public static class OrderPaymentMethods
{
    public const string Qris = "QRIS";
    public const string Cash = "CASH";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Qris, Cash };
}

public static class VoucherLimits
{
    public const int CodeMinLength = 3;
    public const int CodeMaxLength = 30;
    public const int TitleMaxLength = 100;
    public const int DescriptionMaxLength = 300;
}
