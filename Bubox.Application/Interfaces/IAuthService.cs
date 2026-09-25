using Bubox.Application.DTOs.Auth;

namespace Bubox.Application.Interfaces;

public interface IAuthService
{
    Task<MessageResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<MessageResponse> VerifyEmailAsync(VerifyEmailRequest request);
    Task<MessageResponse> ResendOtpAsync(ResendOtpRequest request);
    Task<MessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
}
