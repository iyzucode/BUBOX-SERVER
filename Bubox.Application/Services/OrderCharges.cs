using Bubox.Domain.Constants;

namespace Bubox.Application.Services;

/// <summary>Biaya layanan & pajak sebuah pesanan.</summary>
public readonly record struct OrderChargeAmounts(
    decimal ServiceFee,
    /// <summary>Tarif biaya layanan bila berupa persen; null bila nominal tetap.</summary>
    decimal? ServiceFeePercent,
    decimal TaxAmount,
    decimal TaxPercent);

/// <summary>
/// Perhitungan biaya layanan & pajak. Persentase dibulatkan ke ATAS ke Rupiah penuh.
///   dasar = subtotal − diskon
///   biaya layanan = ceil(dasar × %) atau nominal tetap
///   pajak = ceil((dasar + biaya layanan) × %)
/// Rumus yang sama dipakai client (useCheckoutTotals) untuk perkiraan di checkout.
/// </summary>
public static class OrderCharges
{
    public static OrderChargeAmounts Compute(decimal subtotalAfterDiscount, OrderSettings settings)
    {
        var baseAmount = Math.Max(0m, subtotalAfterDiscount);

        decimal serviceFee;
        decimal? serviceFeePercent = null;
        if (settings.ServiceFeeType == ServiceFeeTypes.Percent)
        {
            serviceFeePercent = settings.ServiceFeeValue;
            serviceFee = CeilPercent(baseAmount, settings.ServiceFeeValue);
        }
        else
        {
            serviceFee = settings.ServiceFeeValue;
        }

        var tax = CeilPercent(baseAmount + serviceFee, settings.TaxPercent);
        return new OrderChargeAmounts(serviceFee, serviceFeePercent, tax, settings.TaxPercent);
    }

    private static decimal CeilPercent(decimal amount, decimal percent) =>
        percent <= 0 ? 0m : Math.Ceiling(amount * percent / 100m);
}
