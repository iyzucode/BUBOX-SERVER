namespace Bubox.Application.Exceptions;

public class AddressNotFoundException(string message = "Alamat tidak ditemukan.")
    : Exception(message);

public class CannotDeletePrimaryAddressException(string message = "Alamat utama tidak dapat dihapus jika masih ada alamat lain. Tetapkan alamat lain sebagai alamat utama terlebih dahulu.")
    : Exception(message);
