using Bubox.Application.Interfaces;

namespace Bubox.Api.BackgroundServices;

/// <summary>
/// Cadangan bila webhook "expire" Midtrans tidak sampai: tiap menit menutup order
/// AWAITING_PAYMENT yang sudah lewat payment_due_at (setelah cek status ke Midtrans).
/// Bisa dimatikan lewat konfigurasi OrderExpiry:Enabled = false.
/// </summary>
public class OrderExpiryHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OrderExpiryHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bool.TryParse(configuration["OrderExpiry:Enabled"], out var enabled) && !enabled)
        {
            logger.LogInformation("OrderExpiryHostedService dinonaktifkan lewat konfigurasi.");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                var count = await paymentService.ExpireOverdueOrdersAsync(stoppingToken);
                if (count > 0)
                {
                    logger.LogInformation("{Count} pesanan yang lewat batas bayar telah diproses.", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Gagal memproses pesanan yang lewat batas bayar.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
