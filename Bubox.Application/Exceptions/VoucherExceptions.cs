namespace Bubox.Application.Exceptions;

public class VoucherNotFoundException(string message = "Kode voucher tidak ditemukan.")
    : Exception(message);

/// <summary>Voucher ada tetapi tidak bisa dipakai untuk pesanan ini (berakhir, minimal belanja, sudah dipakai, ...).</summary>
public class VoucherNotApplicableException(string message)
    : Exception(message);

/// <summary>Data voucher dari admin tidak valid.</summary>
public class InvalidVoucherException(string message)
    : Exception(message);

public class VoucherCodeExistsException(string message = "Kode voucher sudah dipakai voucher lain.")
    : Exception(message);
