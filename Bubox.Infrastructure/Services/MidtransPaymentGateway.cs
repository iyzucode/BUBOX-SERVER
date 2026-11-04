using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bubox.Infrastructure.Services;

/// <summary>
/// Midtrans Core API, khusus QRIS.
/// Konfigurasi: Midtrans:ServerKey, Midtrans:IsProduction, Midtrans:QrisAcquirer (default "gopay").
/// </summary>
public class MidtransPaymentGateway(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MidtransPaymentGateway> logger) : IPaymentGateway
{
    // Waktu di respons Midtrans memakai zona Asia/Jakarta (GMT+7)
    private static readonly TimeSpan MidtransOffset = TimeSpan.FromHours(7);

    private string ServerKey
    {
        get
        {
            var key = configuration["Midtrans:ServerKey"];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new PaymentGatewayException("Midtrans ServerKey belum dikonfigurasi.");
            }

            return key;
        }
    }

    // Midtrans:BaseUrl opsional, hanya untuk pengujian dengan mock server
    private string BaseUrl =>
        configuration["Midtrans:BaseUrl"] is { Length: > 0 } overrideUrl
            ? overrideUrl.TrimEnd('/')
            : bool.TryParse(configuration["Midtrans:IsProduction"], out var isProd) && isProd
                ? "https://api.midtrans.com"
                : "https://api.sandbox.midtrans.com";

    public async Task<QrisChargeResult> CreateQrisChargeAsync(QrisChargeRequest request, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            payment_type = PaymentConstants.PaymentTypeQris,
            transaction_details = new
            {
                order_id = request.OrderId,
                gross_amount = decimal.ToInt64(request.GrossAmount)
            },
            customer_details = new
            {
                first_name = request.CustomerName,
                email = request.CustomerEmail
            },
            qris = new
            {
                acquirer = configuration["Midtrans:QrisAcquirer"] ?? "gopay"
            },
            custom_expiry = new
            {
                expiry_duration = request.ExpiryMinutes,
                unit = "minute"
            }
        };

        using var httpRequest = CreateRequest(HttpMethod.Post, "/v2/charge");
        httpRequest.Content = JsonContent.Create(body);

        using var root = await SendAsync(httpRequest, cancellationToken);
        var json = root.RootElement;
        var statusCode = GetString(json, "status_code");

        if (statusCode is not ("200" or "201"))
        {
            var message = GetString(json, "status_message") ?? "Tidak diketahui";
            logger.LogWarning("Midtrans menolak charge QRIS {OrderId}: {StatusCode} {Message}", request.OrderId, statusCode, message);
            throw new PaymentGatewayException($"Gagal membuat transaksi QRIS: {message}");
        }

        string? qrUrl = null;
        if (json.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                if (GetString(action, "name") == "generate-qr-code")
                {
                    qrUrl = GetString(action, "url");
                    break;
                }
            }
        }

        return new QrisChargeResult(
            TransactionId: GetString(json, "transaction_id"),
            TransactionStatus: GetString(json, "transaction_status") ?? MidtransTransactionStatuses.Pending,
            Acquirer: GetString(json, "acquirer"),
            QrString: GetString(json, "qr_string"),
            QrUrl: qrUrl,
            ExpiresAtUtc: ParseMidtransTime(GetString(json, "expiry_time"))
        );
    }

    public async Task<GatewayTransactionStatus?> GetTransactionStatusAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateRequest(HttpMethod.Get, $"/v2/{Uri.EscapeDataString(orderId)}/status");
        using var root = await SendAsync(httpRequest, cancellationToken);
        var json = root.RootElement;
        var statusCode = GetString(json, "status_code");

        if (statusCode == "404")
        {
            return null;
        }

        var transactionStatus = GetString(json, "transaction_status");
        if (string.IsNullOrEmpty(transactionStatus))
        {
            var message = GetString(json, "status_message") ?? "Tidak diketahui";
            throw new PaymentGatewayException($"Gagal membaca status transaksi: {message}");
        }

        return new GatewayTransactionStatus(
            OrderId: GetString(json, "order_id") ?? orderId,
            TransactionStatus: transactionStatus,
            TransactionId: GetString(json, "transaction_id"),
            PaymentType: GetString(json, "payment_type"),
            FraudStatus: GetString(json, "fraud_status"),
            GrossAmount: decimal.TryParse(GetString(json, "gross_amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var gross) ? gross : 0m,
            Acquirer: GetString(json, "acquirer"),
            SettlementTimeUtc: ParseMidtransTime(GetString(json, "settlement_time"))
        );
    }

    public async Task<bool> TryExpireTransactionAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = CreateRequest(HttpMethod.Post, $"/v2/{Uri.EscapeDataString(orderId)}/expire");
            using var root = await SendAsync(httpRequest, cancellationToken);
            var statusCode = GetString(root.RootElement, "status_code");
            if (statusCode is "200" or "407")
            {
                return true;
            }

            logger.LogWarning("Midtrans tidak meng-expire transaksi {OrderId}: {StatusCode} {Message}",
                orderId, statusCode, GetString(root.RootElement, "status_message"));
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Gagal meng-expire transaksi Midtrans {OrderId}", orderId);
            return false;
        }
    }

    public bool VerifySignature(string orderId, string statusCode, string grossAmount, string signatureKey)
    {
        if (string.IsNullOrEmpty(signatureKey))
        {
            return false;
        }

        var raw = Encoding.UTF8.GetBytes(orderId + statusCode + grossAmount + ServerKey);
        var expected = Convert.ToHexStringLower(SHA512.HashData(raw));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signatureKey.Trim().ToLowerInvariant()));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, BaseUrl + path);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(ServerKey + ":"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Tidak dapat terhubung ke Midtrans ({Url})", request.RequestUri);
            throw new PaymentGatewayException("Tidak dapat terhubung ke layanan pembayaran.", ex);
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogError("Midtrans menolak autentikasi ({Status}). Periksa Midtrans:ServerKey.", (int)response.StatusCode);
                throw new PaymentGatewayException("Konfigurasi layanan pembayaran tidak valid.");
            }

            try
            {
                return JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Respons Midtrans bukan JSON ({Status}): {Body}", (int)response.StatusCode, content);
                throw new PaymentGatewayException("Respons layanan pembayaran tidak valid.", ex);
            }
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            }
            : null;

    private static DateTime? ParseMidtransTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return new DateTimeOffset(local, MidtransOffset).UtcDateTime;
        }

        return null;
    }
}
