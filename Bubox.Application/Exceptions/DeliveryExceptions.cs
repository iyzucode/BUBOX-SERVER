namespace Bubox.Application.Exceptions;

/// <summary>Alamat di luar area pengantaran (atau wilayahnya tidak dikenali).</summary>
public class DeliveryNotCoveredException(string message = "Alamat ini di luar jangkauan pengantaran. Pilih alamat lain atau ambil sendiri.")
    : Exception(message);

/// <summary>Input admin untuk area / zona / driver tidak valid.</summary>
public class InvalidDeliverySettingException(string message)
    : Exception(message);

public class DeliveryZoneNotFoundException(string message = "Zona tidak ditemukan.")
    : Exception(message);

public class DeliveryZoneCodeExistsException(string message = "Kode zona sudah dipakai zona lain.")
    : Exception(message);

/// <summary>Zona masih punya wilayah atau driver aktif sehingga tidak boleh dihapus.</summary>
public class DeliveryZoneNotEmptyException(string message = "Zona masih punya wilayah atau driver. Pindahkan wilayahnya dan lepas drivernya dulu, atau nonaktifkan saja.")
    : Exception(message);

/// <summary>Hari kirim belum punya driver saat akan diberangkatkan.</summary>
public class DriverNotAssignedException(string message = "Kiriman ini belum punya driver. Tetapkan driver dulu di menu Pesanan.")
    : Exception(message);
