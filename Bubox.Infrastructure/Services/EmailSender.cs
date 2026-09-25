using Bubox.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bubox.Infrastructure.Services;

public class EmailSender(ILogger<EmailSender> logger) : IEmailSender
{
    public Task SendOtpAsync(string email, string otpCode, string purpose)
    {
        // For development/testing: Output clearly formatted log to console
        logger.LogInformation(
            "\n==================================================\n" +
            "[EMAIL NOTIFICATION]\n" +
            "To: {Email}\n" +
            "Subject: {Purpose} - Bubox\n" +
            "Kode OTP: >>> {OtpCode} <<<\n" +
            "Masa Berlaku: 10 Menit\n" +
            "==================================================",
            email, purpose, otpCode);

        return Task.CompletedTask;
    }
}
