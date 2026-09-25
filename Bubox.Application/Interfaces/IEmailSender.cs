namespace Bubox.Application.Interfaces;

public interface IEmailSender
{
    Task SendOtpAsync(string email, string otpCode, string purpose);
}
