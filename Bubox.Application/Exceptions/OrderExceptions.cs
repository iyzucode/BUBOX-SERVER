using System.Globalization;
using Bubox.Application.DTOs.Order;

namespace Bubox.Application.Exceptions;

/// <summary>Exception yang membawa data tambahan untuk client (dikirim sebagai field "data" pada respons error).</summary>
public interface IErrorDataException
{
    object? ErrorData { get; }
}

/// <summary>
/// Total yang dihitung server berbeda dengan total yang dilihat customer di checkout (mis. admin baru mengubah
/// ongkir / biaya layanan / pajak). Pesanan TIDAK dibuat; client menampilkan rincian baru untuk dikonfirmasi.
/// </summary>
public class OrderPriceChangedException(decimal expectedGrandTotal, OrderPriceDto current)
    : Exception(
        $"Total pesanan berubah dari Rp {Rupiah(expectedGrandTotal)} menjadi Rp {Rupiah(current.GrandTotal)} karena ada pembaruan biaya. Periksa rinciannya lalu bayar lagi."),
      IErrorDataException
{
    public OrderPriceDto Current { get; } = current;
    public object? ErrorData => Current;

    private static string Rupiah(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("id-ID"));
}

public class OrderNotFoundException(string message = "Pesanan tidak ditemukan.")
    : Exception(message);

public class InvalidOrderException(string message = "Data pesanan tidak valid.")
    : Exception(message);

public class OrderCutoffPassedException(string message)
    : Exception(message);

public class InvalidStatusTransitionException(string message = "Perubahan status tidak diizinkan.")
    : Exception(message);

public class ForbiddenStatusTransitionException(string message = "Anda tidak memiliki akses untuk mengubah status ini.")
    : Exception(message);

public class OrderConcurrencyException(string message = "Status pesanan baru saja berubah. Muat ulang data lalu coba lagi.")
    : Exception(message);

public class PaymentGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class InvalidPaymentNotificationException(string message = "Notifikasi pembayaran tidak valid.")
    : Exception(message);

public class InvalidSettingException(string message)
    : Exception(message);
