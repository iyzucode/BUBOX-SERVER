namespace Bubox.Application.Exceptions;

public class EmailNotVerifiedException(string message = "Email belum diverifikasi. Silakan lakukan verifikasi email terlebih dahulu.")
    : Exception(message);

public class InvalidCredentialsException(string message = "Email/Username atau password tidak valid.")
    : Exception(message);

public class UserAlreadyExistsException(string message = "Email sudah terdaftar dalam sistem.")
    : Exception(message);

public class UsernameAlreadyExistsException(string message = "Username sudah digunakan oleh akun lain.")
    : Exception(message);

public class InvalidUsernameException(string message = "Username harus terdiri dari 3-30 karakter alfanumerik (huruf, angka, atau garis bawah).")
    : Exception(message);

public class InvalidOtpException(string message = "Kode OTP tidak valid atau telah kedaluwarsa.")
    : Exception(message);

public class UserNotFoundException(string message = "Pengguna tidak ditemukan.")
    : Exception(message);

public class OtpAttemptsExceededException(string message = "Terlalu banyak percobaan kode yang salah. Silakan minta kode baru.")
    : Exception(message);

public class InvalidGoogleTokenException(string message = "Login Google gagal. Silakan coba lagi.")
    : Exception(message);

public class PasswordNotSetException(string message = "Akun ini terdaftar lewat Google. Masuk dengan Google, atau buat kata sandi lewat \"Lupa password\".")
    : Exception(message);

public class InvalidAvatarException(string message = "Foto profil harus berupa gambar JPG, PNG, atau WebP.")
    : Exception(message);

public class AvatarTooLargeException(string message)
    : Exception(message);

/// <summary>Data profil masih dalam batas waktu perubahan (nama 7 hari, username 30 hari, foto 7 hari).</summary>
public class ProfileChangeCooldownException(string message)
    : Exception(message);

public class InvalidFullNameException(string message = "Nama harus 2-100 karakter dan hanya berisi huruf, spasi, titik, petik, atau tanda hubung.")
    : Exception(message);

public class InvalidPhoneNumberException(string message = "Nomor harus diawali 08 atau +62 dan terdiri dari 10-14 digit.")
    : Exception(message);
