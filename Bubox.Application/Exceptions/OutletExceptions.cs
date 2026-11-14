namespace Bubox.Application.Exceptions;

/// <summary>Input admin untuk outlet tidak valid.</summary>
public class InvalidOutletException(string message)
    : Exception(message);

public class OutletNotFoundException(string message = "Outlet tidak ditemukan.")
    : Exception(message);

public class OutletNameExistsException(string message = "Nama outlet sudah dipakai outlet lain.")
    : Exception(message);

/// <summary>Outlet sudah dipakai pesanan sehingga tidak boleh dihapus.</summary>
public class OutletInUseException(string message = "Outlet ini sudah dipakai pesanan sehingga tidak bisa dihapus. Nonaktifkan saja agar tidak muncul di pilihan customer.")
    : Exception(message);

/// <summary>Outlet yang dipilih customer tidak ada / sudah nonaktif saat pesanan dibuat.</summary>
public class OutletUnavailableException(string message = "Outlet yang dipilih sudah tidak tersedia. Pilih outlet lain.")
    : Exception(message);
