using System.Text.RegularExpressions;
using Bubox.Application.DTOs.Auth;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Enums;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public partial class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtProvider jwtProvider,
    IEmailSender emailSender) : IAuthService
{
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_]{3,30}$", RegexOptions.Compiled);

    public async Task<MessageResponse> RegisterAsync(RegisterRequest request)
    {
        var cleanedUsername = request.Username.Trim().ToLowerInvariant();
        if (!UsernameRegex.IsMatch(cleanedUsername))
        {
            throw new InvalidUsernameException();
        }

        var existingUsername = await userRepository.GetByUsernameAsync(cleanedUsername);
        if (existingUsername != null)
        {
            throw new UsernameAlreadyExistsException();
        }

        var cleanedEmail = request.Email.Trim().ToLowerInvariant();
        var existingEmail = await userRepository.GetByEmailAsync(cleanedEmail);
        if (existingEmail != null)
        {
            throw new UserAlreadyExistsException();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = cleanedUsername,
            Email = cleanedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await userRepository.CreateAsync(user);

        // Assign default 'Customer' role to newly registered user
        await userRepository.AddUserRoleAsync(user.Id, RoleConstants.Codes.Customer);

        // Generate 6-digit OTP for email verification
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var otp = new UserOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpCode = otpCode,
            Type = OtpType.EmailVerification.ToString(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await userRepository.SaveOtpAsync(otp);
        await emailSender.SendOtpAsync(user.Email, otpCode, "Verifikasi Email");

        return new MessageResponse("Registrasi berhasil. Silakan cek email Anda untuk kode verifikasi 6-digit.");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var identifier = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByIdentifierAsync(identifier);
        if (user == null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        if (!user.IsEmailVerified)
        {
            throw new EmailNotVerifiedException();
        }

        var roles = (await userRepository.GetUserRolesAsync(user.Id)).ToList();
        var token = jwtProvider.GenerateToken(user, roles);
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.FullName, user.IsEmailVerified, roles);

        return new AuthResponse(token, userDto);
    }

    public async Task<MessageResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant())
            ?? throw new UserNotFoundException();

        var otp = await userRepository.GetValidOtpAsync(user.Id, request.OtpCode.Trim(), OtpType.EmailVerification.ToString())
            ?? throw new InvalidOtpException();

        await userRepository.MarkOtpUsedAsync(otp.Id);
        await userRepository.SetEmailVerifiedAsync(user.Id);

        return new MessageResponse("Email berhasil diverifikasi. Silakan login.");
    }

    public async Task<MessageResponse> ResendOtpAsync(ResendOtpRequest request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant())
            ?? throw new UserNotFoundException();

        var otpType = request.Type.Equals("PasswordReset", StringComparison.OrdinalIgnoreCase)
            ? OtpType.PasswordReset.ToString()
            : OtpType.EmailVerification.ToString();

        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var otp = new UserOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpCode = otpCode,
            Type = otpType,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await userRepository.SaveOtpAsync(otp);
        await emailSender.SendOtpAsync(user.Email, otpCode, otpType == OtpType.PasswordReset.ToString() ? "Reset Password" : "Verifikasi Email");

        return new MessageResponse("Kode OTP baru telah dikirim ke email Anda.");
    }

    public async Task<MessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user != null)
        {
            var otpCode = Random.Shared.Next(100000, 999999).ToString();
            var otp = new UserOtp
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OtpCode = otpCode,
                Type = OtpType.PasswordReset.ToString(),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            await userRepository.SaveOtpAsync(otp);
            await emailSender.SendOtpAsync(user.Email, otpCode, "Reset Password");
        }

        return new MessageResponse("Jika email terdaftar, kode OTP untuk reset password telah dikirim.");
    }

    public async Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant())
            ?? throw new UserNotFoundException();

        var otp = await userRepository.GetValidOtpAsync(user.Id, request.OtpCode.Trim(), OtpType.PasswordReset.ToString())
            ?? throw new InvalidOtpException();

        await userRepository.MarkOtpUsedAsync(otp.Id);
        var newPasswordHash = passwordHasher.Hash(request.NewPassword);
        await userRepository.UpdatePasswordAsync(user.Id, newPasswordHash);

        return new MessageResponse("Password berhasil direset. Silakan login dengan password baru Anda.");
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException();

        var roles = (await userRepository.GetUserRolesAsync(userId)).ToList();

        return new UserDto(user.Id, user.Username, user.Email, user.FullName, user.IsEmailVerified, roles);
    }
}
