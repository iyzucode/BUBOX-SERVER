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
