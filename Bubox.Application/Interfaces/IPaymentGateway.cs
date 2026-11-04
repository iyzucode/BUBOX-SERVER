namespace Bubox.Application.Interfaces;

public record QrisChargeRequest(
    string OrderId,
    decimal GrossAmount,
    int ExpiryMinutes,
    string CustomerName,
    string CustomerEmail
);

public record QrisChargeResult(
    string? TransactionId,
    string TransactionStatus,
    string? Acquirer,
    string? QrString,
    string? QrUrl,
    DateTime? ExpiresAtUtc
);

public record GatewayTransactionStatus(
    string OrderId,
    string TransactionStatus,
    string? TransactionId,
    string? PaymentType,
    string? FraudStatus,
    decimal GrossAmount,
    string? Acquirer,
    DateTime? SettlementTimeUtc
);

/// <summary>Abstraksi payment gateway (implementasi: Midtrans Core API, QRIS saja).</summary>
public interface IPaymentGateway
{
    Task<QrisChargeResult> CreateQrisChargeAsync(QrisChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Status transaksi langsung dari gateway. Null jika transaksi tidak ditemukan.</summary>
    Task<GatewayTransactionStatus?> GetTransactionStatusAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>Menutup transaksi yang masih pending (best effort). Gagal hanya dicatat di log.</summary>
    Task<bool> TryExpireTransactionAsync(string orderId, CancellationToken cancellationToken = default);

    bool VerifySignature(string orderId, string statusCode, string grossAmount, string signatureKey);
}
