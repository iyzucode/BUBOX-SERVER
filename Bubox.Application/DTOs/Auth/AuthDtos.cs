namespace Bubox.Application.DTOs.Auth;

public record RegisterRequest(string Username, string Email, string Password, string FullName);
public record LoginRequest(string Email, string Password);
public record VerifyEmailRequest(string Email, string OtpCode);
public record ResendOtpRequest(string Email, string Type);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);

public record UserDto(Guid Id, string Username, string Email, string FullName, bool IsEmailVerified, List<string> Roles);
public record AuthResponse(string Token, UserDto User);
public record MessageResponse(string Message);
