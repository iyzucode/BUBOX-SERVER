using System.Globalization;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;

namespace Bubox.Application.Services;

/// <summary>Aturan hitung & syarat voucher. Satu sumber untuk daftar voucher, cek kode, dan CreateOrder.</summary>
public static class VoucherPricing
{
    /// <summary>
    /// Potongan untuk <paramref name="subtotal"/> menu (ongkir tidak didiskon), dibulatkan ke bawah
    /// ke Rupiah penuh dan tidak pernah melebihi subtotal.
    /// </summary>
    public static decimal CalculateDiscount(Voucher voucher, decimal subtotal)
    {
        if (subtotal <= 0) return 0m;

        var discount = voucher.DiscountType == VoucherDiscountTypes.Percent
            ? subtotal * voucher.DiscountValue / 100m
            : voucher.DiscountValue;

        if (voucher.MaxDiscount is { } cap)
        {
            discount = Math.Min(discount, cap);
        }

        return Math.Max(0m, Math.Floor(Math.Min(discount, subtotal)));
    }

    /// <summary>Alasan voucher tidak bisa dipakai, atau null bila memenuhi semua syarat.</summary>
    public static string? GetIneligibleReason(
        Voucher voucher, decimal subtotal, string paymentMethod, DateTime nowUtc, bool alreadyUsed)
    {
        if (!voucher.IsActive) return "Voucher sudah tidak aktif.";
        if (nowUtc < voucher.StartsAt) return "Voucher belum berlaku.";
        if (nowUtc >= voucher.EndsAt) return "Voucher sudah berakhir.";
        if (alreadyUsed) return "Voucher ini sudah kamu pakai.";
        if (voucher.IsQuotaExhausted) return "Kuota voucher sudah habis.";

        if (!voucher.PaymentMethods.Contains(paymentMethod))
        {
            var methods = string.Join(" / ", voucher.PaymentMethods.Select(PaymentMethodLabel));
            return $"Voucher hanya untuk pembayaran {methods}.";
        }

        if (subtotal < voucher.MinSubtotal)
        {
            return $"Minimal pembelian {FormatRupiah(voucher.MinSubtotal)}. " +
                   $"Tambah {FormatRupiah(voucher.MinSubtotal - subtotal)} lagi.";
        }

        return null;
    }

    public static string PaymentMethodLabel(string method) => method switch
    {
        OrderPaymentMethods.Cash => "Tunai",
        _ => method
    };

    /// <summary>"Rp500.000". Tanpa CultureInfo id-ID agar aman di container mode invariant globalization.</summary>
    public static string FormatRupiah(decimal amount) =>
        "Rp" + Math.Floor(amount).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
}
