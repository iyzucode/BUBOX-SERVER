using Bubox.Domain.Enums;

namespace Bubox.Application.Interfaces;

/// <summary>Isi email OTP. Kode dikirim apa adanya ke pengguna, tetapi tidak pernah disimpan.</summary>
public record OtpEmailMessage(
    string ToEmail,
    string RecipientName,
    string OtpCode,
    OtpType Type,
    TimeSpan Validity);

public interface IEmailSender
{
    /// <summary>Kirim email OTP. Melempar exception bila pengiriman gagal.</summary>
    Task SendOtpAsync(OtpEmailMessage message, CancellationToken cancellationToken = default);
}
