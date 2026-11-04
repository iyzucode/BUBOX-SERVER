using System.Text.Json;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class PaymentService(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    IOrderUnitOfWorkFactory unitOfWorkFactory,
    IPaymentGateway paymentGateway,
    OrderStatusEngine statusEngine) : IPaymentService
{
    private const int ExpiryBatchSize = 50;

    // Jeda sebelum job menandai EXPIRED, memberi waktu webhook settlement yang terlambat
    private static readonly TimeSpan ExpiryGracePeriod = TimeSpan.FromMinutes(1);

    public async Task HandleMidtransNotificationAsync(string rawPayload, CancellationToken cancellationToken = default)
    {
        string? orderId, statusCode, grossAmount, signatureKey, transactionStatus;
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var json = document.RootElement;
            orderId = GetString(json, "order_id");
            statusCode = GetString(json, "status_code");
            grossAmount = GetString(json, "gross_amount");
            signatureKey = GetString(json, "signature_key");
            transactionStatus = GetString(json, "transaction_status");
        }
        catch (JsonException)
        {
            throw new InvalidPaymentNotificationException("Payload notifikasi bukan JSON yang valid.");
        }

        if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(statusCode) || string.IsNullOrEmpty(grossAmount))
        {
            throw new InvalidPaymentNotificationException("Payload notifikasi tidak lengkap.");
        }

        var signatureValid = paymentGateway.VerifySignature(orderId, statusCode, grossAmount, signatureKey ?? string.Empty);
        var payment = await paymentRepository.GetByProviderOrderIdAsync(orderId);

        await paymentRepository.SaveNotificationAsync(new PaymentNotification
        {
            Id = Guid.NewGuid(),
            PaymentId = payment?.Id,
            ProviderOrderId = orderId,
            TransactionStatus = transactionStatus,
            SignatureValid = signatureValid,
            RawPayload = rawPayload,
            ReceivedAt = DateTime.UtcNow
        });

        if (!signatureValid)
        {
            throw new InvalidPaymentNotificationException("Signature notifikasi tidak valid.");
        }

        if (payment == null)
        {
            // Bukan transaksi dari sistem ini (atau payment belum tersimpan). Dicatat saja, tidak diproses.
            return;
        }

        // Isi webhook tidak dipercaya begitu saja: status dikonfirmasi ulang ke Midtrans
        var status = await paymentGateway.GetTransactionStatusAsync(orderId, cancellationToken)
            ?? throw new InvalidPaymentNotificationException("Transaksi tidak ditemukan di Midtrans.");

        await ApplyGatewayStatusAsync(payment.OrderId, status, DateTime.UtcNow);
    }

    public async Task<int> ExpireOverdueOrdersAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var orderIds = (await orderRepository.GetOverdueAwaitingPaymentIdsAsync(nowUtc - ExpiryGracePeriod, ExpiryBatchSize)).ToList();
        var processed = 0;

        foreach (var orderId in orderIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payment = await paymentRepository.GetByOrderIdAsync(orderId);
            GatewayTransactionStatus? status = null;

            if (payment != null)
            {
                try
                {
                    status = await paymentGateway.GetTransactionStatusAsync(payment.ProviderOrderId, cancellationToken);
                }
                catch (PaymentGatewayException)
                {
                    // Gateway tidak bisa dihubungi: tetap expire. Jika ternyata sudah terbayar,
                    // webhook settlement berikutnya akan menandai payment needs_review.
                }
            }

            if (status != null && IsPaid(status))
            {
                await ApplyGatewayStatusAsync(orderId, status, DateTime.UtcNow);
                processed++;
                continue;
            }

            string? orderNumber = null;
            await using (var uow = await unitOfWorkFactory.BeginAsync())
            {
                var order = await uow.LockOrderAsync(orderId);
                if (order == null || order.StatusCode != OrderStatusCodes.AwaitingPayment)
                {
                    continue;
                }

                var days = await uow.GetDaysAsync(order.Id);
                var target = status != null && IsFailed(status)
                    ? OrderStatusCodes.PaymentFailed
                    : OrderStatusCodes.Expired;

                await statusEngine.CloseUnpaidOrderAsync(uow, order, days, target, OrderActor.System(),
                    "Melewati batas waktu pembayaran.", DateTime.UtcNow);

                var lockedPayment = await uow.GetPaymentAsync(order.Id);
                if (lockedPayment != null && status != null)
                {
                    lockedPayment.TransactionStatus = status.TransactionStatus;
                    lockedPayment.UpdatedAt = DateTime.UtcNow;
                    await uow.UpdatePaymentAsync(lockedPayment);
                }

                await uow.CommitAsync();
                orderNumber = order.OrderNumber;
            }

            if (status?.TransactionStatus == MidtransTransactionStatuses.Pending && orderNumber != null)
            {
                await paymentGateway.TryExpireTransactionAsync(orderNumber, cancellationToken);
            }

            processed++;
        }

        return processed;
    }

    /// <summary>Menerapkan status transaksi dari Midtrans ke payment dan order (idempoten).</summary>
    private async Task ApplyGatewayStatusAsync(Guid orderId, GatewayTransactionStatus status, DateTime nowUtc)
    {
        await using var uow = await unitOfWorkFactory.BeginAsync();
        var order = await uow.LockOrderAsync(orderId);
        var payment = await uow.GetPaymentAsync(orderId);
        if (order == null || payment == null)
        {
            return;
        }

        payment.TransactionStatus = status.TransactionStatus;
        payment.TransactionId ??= status.TransactionId;
        payment.Acquirer ??= status.Acquirer;
        payment.UpdatedAt = nowUtc;

        if (IsPaid(status))
        {
            payment.PaidAt ??= status.SettlementTimeUtc ?? nowUtc;

            if (status.GrossAmount != payment.GrossAmount)
            {
                MarkReview(payment, $"Nominal dibayar {status.GrossAmount:0} tidak sama dengan tagihan {payment.GrossAmount:0}.");
            }
            else if (order.StatusCode == OrderStatusCodes.AwaitingPayment)
            {
                var days = await uow.GetDaysAsync(order.Id);
                await statusEngine.MarkPaidAsync(uow, order, days, nowUtc, "Pembayaran QRIS diterima.");
            }
            else if (!OrderStatusCodes.PaidOrLater.Contains(order.StatusCode))
            {
                // Order sudah ditutup (batal/expired) tetapi uang masuk: refund manual
                MarkReview(payment, $"Pembayaran diterima saat pesanan berstatus {order.StatusCode}. Perlu refund manual.");
            }
        }
        else if (order.StatusCode == OrderStatusCodes.AwaitingPayment)
        {
            if (status.TransactionStatus == MidtransTransactionStatuses.Expire)
            {
                var days = await uow.GetDaysAsync(order.Id);
                await statusEngine.CloseUnpaidOrderAsync(uow, order, days, OrderStatusCodes.Expired, OrderActor.System(),
                    "Transaksi QRIS kedaluwarsa.", nowUtc);
            }
            else if (IsFailed(status))
            {
                var days = await uow.GetDaysAsync(order.Id);
                await statusEngine.CloseUnpaidOrderAsync(uow, order, days, OrderStatusCodes.PaymentFailed, OrderActor.System(),
                    $"Transaksi QRIS {status.TransactionStatus}.", nowUtc);
            }
        }

        await uow.UpdatePaymentAsync(payment);
        await uow.CommitAsync();
    }

    private static bool IsPaid(GatewayTransactionStatus status) =>
        status.TransactionStatus == MidtransTransactionStatuses.Settlement
        || (status.TransactionStatus == MidtransTransactionStatuses.Capture && status.FraudStatus is null or "accept");

    private static bool IsFailed(GatewayTransactionStatus status) =>
        status.TransactionStatus is MidtransTransactionStatuses.Deny
            or MidtransTransactionStatuses.Cancel
            or MidtransTransactionStatuses.Failure;

    private static void MarkReview(Payment payment, string note)
    {
        payment.NeedsReview = true;
        payment.ReviewNote = note.Length > 500 ? note[..500] : note;
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
}
