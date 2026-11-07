namespace Bubox.Api;

/// <summary>Nama kebijakan rate limiting (lihat AddRateLimiter di Program.cs).</summary>
public static class RateLimitPolicies
{
    /// <summary>Endpoint auth: 10 permintaan per menit per IP.</summary>
    public const string Auth = "auth";
}
