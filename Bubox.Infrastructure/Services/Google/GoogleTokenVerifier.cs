using Bubox.Application.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bubox.Infrastructure.Services.Google;

/// <summary>Verifikasi ID token Google memakai library resmi Google.Apis.Auth (kunci publik Google di-cache).</summary>
public class GoogleTokenVerifier(IOptions<GoogleAuthOptions> options, ILogger<GoogleTokenVerifier> logger) : IGoogleTokenVerifier
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (_options.ClientIds.Length == 0)
        {
            logger.LogError("Login Google ditolak: Google:ClientIds belum dikonfigurasi");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _options.ClientIds,
                IssuedAtClockTolerance = TimeSpan.FromMinutes(1),
                ExpirationTimeClockTolerance = TimeSpan.FromMinutes(1),
            });

            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("ID token Google tidak valid: {Reason}", ex.Message);
            return null;
        }
    }
}
