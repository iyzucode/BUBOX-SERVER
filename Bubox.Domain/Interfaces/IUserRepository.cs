using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdentifierAsync(string identifier);
    Task<Guid> CreateAsync(User user);
    /// <summary>Ganti kata sandi; null = hapus (akun hanya bisa masuk lewat penyedia luar).</summary>
    Task UpdatePasswordAsync(Guid userId, string? passwordHash);
    Task SetEmailVerifiedAsync(Guid userId);

    /// <summary>Simpan OTP baru dan batalkan semua OTP aktif lain dengan user & tipe yang sama (satu transaksi).</summary>
    Task ReplaceActiveOtpAsync(UserOtp otp);

    /// <summary>OTP terbaru yang belum dipakai dan belum kedaluwarsa.</summary>
    Task<UserOtp?> GetActiveOtpAsync(Guid userId, string type);

    /// <summary>Tambah hitungan salah tebak secara atomik; mengembalikan hitungan terbaru.</summary>
    Task<int> IncrementOtpAttemptAsync(Guid otpId);

    /// <summary>Tandai OTP terpakai. False bila sudah terpakai lebih dulu (permintaan ganda).</summary>
    Task<bool> TryMarkOtpUsedAsync(Guid otpId);

    Task<OtpSendStats> GetOtpSendStatsAsync(Guid userId, string type, DateTime since);
    Task<IEnumerable<string>> GetUserRolesAsync(Guid userId);

    /// <summary>User yang tertaut ke akun penyedia luar (mis. Google "sub").</summary>
    Task<User?> GetByExternalLoginAsync(string provider, string providerUserId);

    /// <summary>Tautkan akun penyedia luar ke user yang sudah ada.</summary>
    Task AddExternalLoginAsync(UserExternalLogin login);

    /// <summary>Catat waktu masuk terakhir lewat penyedia luar.</summary>
    Task TouchExternalLoginAsync(string provider, string providerUserId);

    /// <summary>Buat user baru + peran + tautan penyedia luar dalam satu transaksi.</summary>
    Task CreateWithExternalLoginAsync(User user, string roleCode, UserExternalLogin login);
    Task AddUserRoleAsync(Guid userId, string roleCode);
}
