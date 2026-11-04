namespace Bubox.Application.Exceptions;

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
