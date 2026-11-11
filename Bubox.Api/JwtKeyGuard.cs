namespace Bubox.Api;

/// <summary>
/// Memastikan kunci penanda tangan JWT diisi dengan benar sebelum API berjalan.
/// Tanpa ini, API diam-diam memakai kunci bawaan yang tertulis di repo, sehingga siapa pun
/// yang pernah melihat source bisa membuat token login palsu (termasuk peran admin).
/// </summary>
public static class JwtKeyGuard
{
    private const int MinLength = 32;

    /// <summary>Kunci yang pernah ter-commit di repo; tidak boleh dipakai di luar Development.</summary>
    private static readonly string[] PublishedKeys =
    [
        "SuperSecretKeyForBuboxPlatformAuthentication2026!MustBe32CharsLong",
        "DefaultFallbackSuperSecretKeyBubox2026!MustBeAtLeast32Chars",
    ];

    public static string Require(IConfiguration configuration, IHostEnvironment environment)
    {
        var key = configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Jwt:Key belum diisi. Produksi: set environment variable Jwt__Key (acak, minimal 32 karakter). " +
                "Lokal: isi Jwt.Key di configuration.Development.yml.");
        }

        if (key.Length < MinLength)
        {
            throw new InvalidOperationException($"Jwt:Key terlalu pendek; minimal {MinLength} karakter.");
        }

        if (!environment.IsDevelopment() && PublishedKeys.Contains(key))
        {
            throw new InvalidOperationException(
                "Jwt:Key memakai kunci contoh yang pernah ada di repo. Buat kunci baru yang acak untuk produksi.");
        }

        return key;
    }
}
