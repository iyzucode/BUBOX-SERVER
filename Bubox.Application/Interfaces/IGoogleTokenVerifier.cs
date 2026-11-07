namespace Bubox.Application.Interfaces;

/// <summary>Data akun Google dari ID token yang sudah diverifikasi.</summary>
public record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

public interface IGoogleTokenVerifier
{
    /// <summary>
    /// Verifikasi ID token Google (tanda tangan, audience = Client ID kita, masa berlaku).
    /// Mengembalikan null bila token tidak valid.
    /// </summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken = default);
}
