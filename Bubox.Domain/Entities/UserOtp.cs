namespace Bubox.Domain.Entities;

public class UserOtp
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 (heksadesimal) dari kode OTP + id user. Kode asli tidak pernah disimpan.</summary>
    public string OtpHash { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }

    /// <summary>Jumlah salah tebak untuk OTP ini.</summary>
    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Ringkasan pengiriman OTP untuk menghitung jeda kirim ulang dan kuota per jam.</summary>
public record OtpSendStats(DateTime? LastCreatedAt, int CountInWindow);
