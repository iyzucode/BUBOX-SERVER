namespace Bubox.Domain.Entities;

/// <summary>Akun penyedia luar (mis. Google) yang tertaut ke user BUBOX.</summary>
public class UserExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
}

public static class ExternalLoginProviders
{
    public const string Google = "GOOGLE";
}
