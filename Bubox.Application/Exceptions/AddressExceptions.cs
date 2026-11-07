namespace Bubox.Application.Exceptions;

public class AddressNotFoundException(string message = "Alamat tidak ditemukan.")
    : Exception(message);

public class CannotDeletePrimaryAddressException(string message = "Alamat utama tidak dapat dihapus jika masih ada alamat lain. Tetapkan alamat lain sebagai alamat utama terlebih dahulu.")
    : Exception(message);

public class InvalidCoordinatesException(string message = "Titik lokasi tidak valid.")
    : Exception(message);

public class LocationNotFoundException(string message = "Alamat di titik ini tidak ditemukan. Geser pin ke lokasi lain.")
    : Exception(message);

public class GeocodingUnavailableException(string message = "Layanan peta sedang tidak tersedia. Isi alamat secara manual atau coba lagi nanti.")
    : Exception(message);
