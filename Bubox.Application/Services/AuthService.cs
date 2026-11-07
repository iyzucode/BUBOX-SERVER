using System.Text.RegularExpressions;
using Bubox.Application.DTOs.Auth;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Enums;
using Bubox.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bubox.Application.Services;

public partial class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtProvider jwtProvider,
    IEmailSender emailSender,
    ILogger<AuthService> logger) : IAuthService
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

        var cleanedEmail = NormalizeEmail(request.Email);
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

        // Kirim OTP verifikasi. Bila email gagal terkirim, registrasi tetap berhasil;
        // pengguna bisa memakai "Kirim ulang" di layar verifikasi.
        await IssueOtpAsync(user, OtpType.EmailVerification);

        return new MessageResponse("Registrasi berhasil. Kode verifikasi 6 digit sudah dikirim ke email kamu.");
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

        return await BuildAuthResponseAsync(user);
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await userRepository.GetByEmailAsync(NormalizeEmail(request.Email));
        if (user is null || user.IsEmailVerified)
        {
            throw new InvalidOtpException();
        }

        await ConsumeOtpAsync(user, request.OtpCode, OtpType.EmailVerification);
        await userRepository.SetEmailVerifiedAsync(user.Id);
        user.IsEmailVerified = true;

        // Langsung masuk setelah verifikasi, tanpa perlu login ulang.
        var auth = await BuildAuthResponseAsync(user);
        return new VerifyEmailResponse("Email berhasil diverifikasi. Selamat datang di LUANNA!", auth.Token, auth.User);
    }

    public async Task<MessageResponse> ResendOtpAsync(ResendOtpRequest request)
    {
        var type = request.Type.Equals(nameof(OtpType.PasswordReset), StringComparison.OrdinalIgnoreCase)
            ? OtpType.PasswordReset
            : OtpType.EmailVerification;

        var user = await userRepository.GetByEmailAsync(NormalizeEmail(request.Email));
        if (user is not null && (type == OtpType.PasswordReset || !user.IsEmailVerified))
        {
            await IssueOtpAsync(user, type);
        }

        // Jawaban selalu sama agar tidak bisa dipakai menebak email mana yang terdaftar.
        return new MessageResponse("Jika email terdaftar, kode baru sudah dikirim. Cek juga folder spam.");
    }

    public async Task<MessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await userRepository.GetByEmailAsync(NormalizeEmail(request.Email));
        if (user is not null)
        {
            await IssueOtpAsync(user, OtpType.PasswordReset);
        }

        return new MessageResponse("Jika email terdaftar, kode untuk reset kata sandi sudah dikirim.");
    }

    public async Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await userRepository.GetByEmailAsync(NormalizeEmail(request.Email))
            ?? throw new InvalidOtpException();

        await ConsumeOtpAsync(user, request.OtpCode, OtpType.PasswordReset);
        await userRepository.UpdatePasswordAsync(user.Id, passwordHasher.Hash(request.NewPassword));

        // Kode reset membuktikan kepemilikan email, jadi email dianggap terverifikasi.
        if (!user.IsEmailVerified)
        {
            await userRepository.SetEmailVerifiedAsync(user.Id);
        }

        return new MessageResponse("Kata sandi berhasil diubah. Silakan masuk dengan kata sandi baru.");
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException();

        var roles = (await userRepository.GetUserRolesAsync(userId)).ToList();

        return new UserDto(user.Id, user.Username, user.Email, user.FullName, user.IsEmailVerified, roles);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        var roles = (await userRepository.GetUserRolesAsync(user.Id)).ToList();
        var token = jwtProvider.GenerateToken(user, roles);
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.FullName, user.IsEmailVerified, roles);
        return new AuthResponse(token, userDto);
    }

    /// <summary>
    /// Buat OTP baru (OTP lama otomatis batal) lalu kirim lewat email.
    /// Dilewati tanpa error bila masih dalam jeda kirim ulang atau kuota per jam habis,
    /// supaya respons endpoint tidak membocorkan status akun.
    /// </summary>
    private async Task IssueOtpAsync(User user, OtpType type)
    {
        var typeKey = type.ToString();
        var now = DateTime.UtcNow;

        var stats = await userRepository.GetOtpSendStatsAsync(user.Id, typeKey, now - OtpPolicy.SendWindow);
        if (stats.LastCreatedAt is { } last && now - last < OtpPolicy.ResendCooldown)
        {
            logger.LogInformation("OTP {Type} untuk user {UserId} dilewati: masih dalam jeda kirim ulang", typeKey, user.Id);
            return;
        }

        if (stats.CountInWindow >= OtpPolicy.MaxSendsPerWindow)
        {
            logger.LogWarning("OTP {Type} untuk user {UserId} dilewati: kuota {Max} per jam habis", typeKey, user.Id, OtpPolicy.MaxSendsPerWindow);
            return;
        }

        var code = OtpCodes.Generate();
        await userRepository.ReplaceActiveOtpAsync(new UserOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = OtpCodes.Hash(user.Id, code),
            Type = typeKey,
            ExpiresAt = now.Add(OtpPolicy.Validity),
            CreatedAt = now
        });

        try
        {
            await emailSender.SendOtpAsync(new OtpEmailMessage(user.Email, user.FullName, code, type, OtpPolicy.Validity));
        }
        catch (Exception ex)
        {
            // Jangan gagalkan permintaan; pengguna bisa minta kirim ulang setelah jeda.
            logger.LogError(ex, "Gagal mengirim email OTP {Type} untuk user {UserId}", typeKey, user.Id);
        }
    }

    /// <summary>Cocokkan lalu pakai OTP. Salah tebak dihitung; setelah batas tercapai OTP hangus.</summary>
    private async Task ConsumeOtpAsync(User user, string input, OtpType type)
    {
        if (!OtpCodes.TryNormalize(input, out var code))
        {
            throw new InvalidOtpException("Kode OTP harus 6 digit angka.");
        }

        var otp = await userRepository.GetActiveOtpAsync(user.Id, type.ToString())
            ?? throw new InvalidOtpException();

        if (otp.AttemptCount >= OtpPolicy.MaxAttempts)
        {
            await userRepository.TryMarkOtpUsedAsync(otp.Id);
            throw new OtpAttemptsExceededException();
        }

        if (!OtpCodes.Matches(otp.OtpHash, user.Id, code))
        {
            var attempts = await userRepository.IncrementOtpAttemptAsync(otp.Id);
            var remaining = OtpPolicy.MaxAttempts - attempts;
            if (remaining <= 0)
            {
                await userRepository.TryMarkOtpUsedAsync(otp.Id);
                throw new OtpAttemptsExceededException();
            }

            throw new InvalidOtpException($"Kode OTP salah. Sisa {remaining} percobaan lagi.");
        }

        // Atomik: dua permintaan bersamaan dengan kode yang sama hanya satu yang lolos.
        if (!await userRepository.TryMarkOtpUsedAsync(otp.Id))
        {
            throw new InvalidOtpException();
        }
    }
}
