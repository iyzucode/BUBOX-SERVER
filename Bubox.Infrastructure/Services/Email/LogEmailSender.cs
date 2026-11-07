using Bubox.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bubox.Infrastructure.Services.Email;

/// <summary>
/// Mode "Log": tidak mengirim email, hanya menulis kode OTP ke log server.
/// Hanya untuk development lokal; jangan dipakai di server produksi.
/// </summary>
public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendOtpAsync(OtpEmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "\n==================================================\n" +
            "[EMAIL MODE LOG - TIDAK DIKIRIM]\n" +
            "To: {Email}\n" +
            "Tipe: {Type}\n" +
            "Kode OTP: >>> {OtpCode} <<<\n" +
            "Masa Berlaku: {Minutes} menit\n" +
            "==================================================",
            message.ToEmail, message.Type, message.OtpCode, (int)message.Validity.TotalMinutes);

        return Task.CompletedTask;
    }
}
