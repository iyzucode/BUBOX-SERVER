namespace Bubox.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderOrderId { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string TransactionStatus { get; set; } = string.Empty;
    public string? Acquirer { get; set; }
    public string? QrString { get; set; }
    public string? QrUrl { get; set; }
    public decimal GrossAmount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public bool NeedsReview { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PaymentNotification
{
    public Guid Id { get; set; }
    public Guid? PaymentId { get; set; }
    public string? ProviderOrderId { get; set; }
    public string? TransactionStatus { get; set; }
    public bool SignatureValid { get; set; }
    public string RawPayload { get; set; } = string.Empty; // JSON
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class AppSetting
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
