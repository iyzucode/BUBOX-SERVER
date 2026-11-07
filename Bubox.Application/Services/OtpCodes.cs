using System.Security.Cryptography;
using System.Text;
using Bubox.Domain.Constants;

namespace Bubox.Application.Services;

/// <summary>Pembuatan dan pencocokan kode OTP. Kode asli hanya ada di memori dan di email pengguna.</summary>
public static class OtpCodes
{
    /// <summary>Kode 6 digit dari generator acak kriptografis (bukan System.Random).</summary>
    public static string Generate()
    {
        var max = (int)Math.Pow(10, OtpPolicy.CodeLength);
        return RandomNumberGenerator.GetInt32(0, max).ToString($"D{OtpPolicy.CodeLength}");
    }

    /// <summary>SHA-256(userId:kode) dalam heksadesimal huruf kecil (64 karakter).</summary>
    public static string Hash(Guid userId, string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{code}"));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Bandingkan dengan waktu konstan agar tidak bocor lewat perbedaan waktu respons.</summary>
    public static bool Matches(string storedHash, Guid userId, string code)
    {
        var expected = Encoding.ASCII.GetBytes(storedHash);
        var actual = Encoding.ASCII.GetBytes(Hash(userId, code));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>Normalisasi input pengguna: buang spasi; valid hanya bila tepat 6 digit angka.</summary>
    public static bool TryNormalize(string? input, out string code)
    {
        code = new string((input ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        return code.Length == OtpPolicy.CodeLength && code.All(char.IsAsciiDigit);
    }
}
