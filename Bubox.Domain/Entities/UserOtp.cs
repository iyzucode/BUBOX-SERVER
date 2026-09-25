namespace Bubox.Domain.Entities;

public class UserOtp
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OtpCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
