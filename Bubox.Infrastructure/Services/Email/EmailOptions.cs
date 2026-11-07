namespace Bubox.Infrastructure.Services.Email;

/// <summary>
/// Konfigurasi pengiriman email (section "Email" di configuration.yml).
/// Mode "Log" hanya menulis email ke log (untuk development tanpa kirim sungguhan);
/// mode "Smtp" mengirim lewat server SMTP mana pun (saat ini Resend: smtp.resend.com).
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string Mode { get; set; } = EmailModes.Log;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "LUANNA";
    public SmtpOptions Smtp { get; set; } = new();
}

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 465;
    public string Username { get; set; } = string.Empty;

    /// <summary>Untuk Resend: API key. Jangan commit; isi lewat environment variable Email__Smtp__Password.</summary>
    public string Password { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;
}

public static class EmailModes
{
    public const string Log = "Log";
    public const string Smtp = "Smtp";
}
