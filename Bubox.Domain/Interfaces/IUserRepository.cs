using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdentifierAsync(string identifier);
    Task<Guid> CreateAsync(User user);
    Task UpdatePasswordAsync(Guid userId, string passwordHash);
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
    Task AddUserRoleAsync(Guid userId, string roleCode);
}
