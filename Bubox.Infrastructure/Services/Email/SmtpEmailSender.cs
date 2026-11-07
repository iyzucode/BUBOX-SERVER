using Bubox.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bubox.Infrastructure.Services.Email;

/// <summary>Mengirim email lewat SMTP (Resend: smtp.resend.com, username "resend", password = API key).</summary>
public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendOtpAsync(OtpEmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.RecipientName, message.ToEmail));
        mime.Subject = OtpEmailTemplate.Subject(message);
        mime.Body = new BodyBuilder
        {
            TextBody = OtpEmailTemplate.PlainText(message),
            HtmlBody = OtpEmailTemplate.Html(message),
        }.ToMessageBody();

        var smtp = _options.Smtp;
        using var client = new SmtpClient { Timeout = smtp.TimeoutSeconds * 1000 };

        // Port 465 = SSL langsung; 587/2587 = STARTTLS. Auto memilih sesuai port.
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.Auto, cancellationToken);
        await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Email OTP {Type} terkirim ke {Email}", message.Type, MaskEmail(message.ToEmail));
    }

    /// <summary>Samarkan email di log: budi@gmail.com → b***@gmail.com.</summary>
    internal static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" + email[Math.Max(at, 0)..] : email[0] + "***" + email[at..];
    }
}
