namespace Bubox.Application.DTOs.Auth;

public record RegisterRequest(string Username, string Email, string Password, string FullName);
public record LoginRequest(string Email, string Password);
public record VerifyEmailRequest(string Email, string OtpCode);
public record ResendOtpRequest(string Email, string Type);
public record ForgotPasswordRequest(string Email);
/// <summary>ID token dari Google Sign-In (bukan access token).</summary>
public record GoogleLoginRequest(string IdToken);

public record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);

/// <param name="AvatarUrl">Path relatif API foto profil (berversi); null = tampilkan huruf awal nama.</param>
public record UserDto(Guid Id, string Username, string Email, string FullName, bool IsEmailVerified, List<string> Roles, string? AvatarUrl);
public record AuthResponse(string Token, UserDto User);
public record MessageResponse(string Message);

/// <summary>Hasil verifikasi email: akun langsung masuk (token + user).</summary>
public record VerifyEmailResponse(string Message, string Token, UserDto User);
