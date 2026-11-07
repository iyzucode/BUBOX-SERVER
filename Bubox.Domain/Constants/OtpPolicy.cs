namespace Bubox.Domain.Constants;

/// <summary>Aturan OTP email (verifikasi akun & reset password).</summary>
public static class OtpPolicy
{
    public const int CodeLength = 6;

    /// <summary>Masa berlaku satu kode OTP.</summary>
    public static readonly TimeSpan Validity = TimeSpan.FromMinutes(10);

    /// <summary>Batas salah tebak; setelah itu OTP hangus dan harus minta kode baru.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Jeda minimal antar pengiriman OTP untuk email & tipe yang sama.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Kuota pengiriman OTP per email & tipe dalam <see cref="SendWindow"/>.</summary>
    public const int MaxSendsPerWindow = 5;
    public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);
}
