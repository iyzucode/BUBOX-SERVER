using System.Globalization;
using System.Text.RegularExpressions;
using Bubox.Application.DTOs.Auth;
using Bubox.Application.DTOs.Profile;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Bubox.Application.Services;

public class ProfileService(IUserRepository userRepository, IAuthService authService, IConfiguration configuration)
    : IProfileService
{
    private const int DefaultMaxAvatarBytes = 2 * 1024 * 1024;

    private static readonly Regex UsernameRegex = new(@"^[a-z0-9_]{3,30}$", RegexOptions.Compiled);
    private static readonly Regex FullNameRegex = new(@"^\p{L}[\p{L}\p{M} .'\-]{1,99}$", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex PhoneSeparatorsRegex = new(@"[\s\-().]", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^08\d{8,12}$", RegexOptions.Compiled);

    private static readonly CultureInfo IndonesianCulture = CultureInfo.GetCultureInfo("id-ID");
    private static readonly TimeZoneInfo Wib = ResolveWib();

    public int MaxAvatarBytes =>
        int.TryParse(configuration["Profile:MaxAvatarBytes"], out var configured) && configured > 0
            ? configured
            : DefaultMaxAvatarBytes;

    public async Task<ProfileDto> GetProfileAsync(Guid userId) => ToProfileDto(await GetUserAsync(userId));

    public async Task<ProfileDto> UpdateFullNameAsync(Guid userId, string fullName)
    {
        var cleaned = WhitespaceRegex.Replace(fullName?.Trim() ?? string.Empty, " ");
        if (!FullNameRegex.IsMatch(cleaned))
        {
            throw new InvalidFullNameException();
        }

        var user = await GetUserAsync(userId);
        if (cleaned == user.FullName)
        {
            return ToProfileDto(user); // Tidak ada perubahan: jangan habiskan jatah 7 hari.
        }

        EnsureCanChange(user.FullNameChangedAt, ProfilePolicy.FullNameChangeInterval, "nama");
        if (!await userRepository.TryUpdateFullNameAsync(userId, cleaned, ChangeAllowedBefore(ProfilePolicy.FullNameChangeInterval)))
        {
            // Permintaan lain mengubah nama tepat sebelum ini.
            user = await GetUserAsync(userId);
            EnsureCanChange(user.FullNameChangedAt, ProfilePolicy.FullNameChangeInterval, "nama");
        }

        return await GetProfileAsync(userId);
    }

    public async Task<ProfileDto> UpdateUsernameAsync(Guid userId, string username)
    {
        var cleaned = (username ?? string.Empty).Trim().TrimStart('@').ToLowerInvariant();
        if (!UsernameRegex.IsMatch(cleaned))
        {
            throw new InvalidUsernameException();
        }

        var user = await GetUserAsync(userId);
        if (cleaned == user.Username)
        {
            return ToProfileDto(user);
        }

        EnsureCanChange(user.UsernameChangedAt, ProfilePolicy.UsernameChangeInterval, "username");

        var owner = await userRepository.GetByUsernameAsync(cleaned);
        if (owner is not null && owner.Id != userId)
        {
            throw new UsernameAlreadyExistsException();
        }

        var result = await userRepository.TryUpdateUsernameAsync(
            userId, cleaned, ChangeAllowedBefore(ProfilePolicy.UsernameChangeInterval));
        switch (result)
        {
            case UsernameUpdateResult.Taken:
                throw new UsernameAlreadyExistsException();
            case UsernameUpdateResult.CooldownActive:
                user = await GetUserAsync(userId);
                EnsureCanChange(user.UsernameChangedAt, ProfilePolicy.UsernameChangeInterval, "username");
                break;
        }

        return await GetProfileAsync(userId);
    }

    public async Task<ProfileDto> UpdateContactAsync(Guid userId, UpdateContactRequest request)
    {
        var phone = NormalizePhone(request.PhoneNumber);
        var whatsapp = NormalizePhone(request.WhatsappNumber);

        _ = await GetUserAsync(userId);
        await userRepository.UpdateContactAsync(userId, phone, whatsapp);
        return await GetProfileAsync(userId);
    }

    public async Task<UserDto> UpdateAvatarAsync(Guid userId, byte[] data)
    {
        if (data.Length == 0)
        {
            throw new InvalidAvatarException("File foto kosong.");
        }

        if (data.Length > MaxAvatarBytes)
        {
            throw new AvatarTooLargeException(
                $"Ukuran foto maksimal {MaxAvatarBytes / (1024 * 1024.0):0.#} MB.");
        }

        var contentType = AvatarImage.DetectContentType(data)
            ?? throw new InvalidAvatarException();

        var user = await GetUserAsync(userId);
        EnsureCanChange(user.AvatarChangedAt, ProfilePolicy.AvatarChangeInterval, "foto profil");

        var saved = await userRepository.TryUpsertAvatarAsync(
            new UserAvatar
            {
                UserId = userId,
                ContentType = contentType,
                Data = data,
                UpdatedAt = DateTime.UtcNow,
            },
            ChangeAllowedBefore(ProfilePolicy.AvatarChangeInterval));
        if (!saved)
        {
            user = await GetUserAsync(userId);
            EnsureCanChange(user.AvatarChangedAt, ProfilePolicy.AvatarChangeInterval, "foto profil");
        }

        return await authService.GetCurrentUserAsync(userId);
    }

    /// <summary>Hapus foto selalu boleh; jatah ganti foto (7 hari) tidak ikut direset.</summary>
    public async Task<UserDto> RemoveAvatarAsync(Guid userId)
    {
        await userRepository.DeleteAvatarAsync(userId);
        return await authService.GetCurrentUserAsync(userId);
    }

    public Task<UserAvatar?> GetAvatarAsync(Guid userId) => userRepository.GetAvatarAsync(userId);

    private async Task<User> GetUserAsync(Guid userId) =>
        await userRepository.GetByIdAsync(userId) ?? throw new UserNotFoundException();

    private static DateTime ChangeAllowedBefore(TimeSpan interval) => DateTime.UtcNow - interval;

    private static DateTime? NextChangeAt(DateTime? lastChangedAt, TimeSpan interval)
    {
        if (lastChangedAt is not { } last) return null;
        var next = last.ToUniversalTime() + interval;
        return next > DateTime.UtcNow ? next : null;
    }

    private static void EnsureCanChange(DateTime? lastChangedAt, TimeSpan interval, string fieldLabel)
    {
        if (NextChangeAt(lastChangedAt, interval) is { } next)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(next, Wib);
            throw new ProfileChangeCooldownException(
                $"Kamu baru bisa mengubah {fieldLabel} lagi pada {local.ToString("d MMMM yyyy, HH.mm", IndonesianCulture)} WIB.");
        }
    }

    /// <summary>"+62 812-3456-7890" / "62812..." / "0812..." → "0812..."; kosong → null.</summary>
    private static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = PhoneSeparatorsRegex.Replace(raw.Trim(), string.Empty);
        if (digits.StartsWith("+62")) digits = "0" + digits[3..];
        else if (digits.StartsWith("62")) digits = "0" + digits[2..];

        return PhoneRegex.IsMatch(digits) ? digits : throw new InvalidPhoneNumberException();
    }

    private ProfileDto ToProfileDto(User user)
    {
        var userDto = UserDtoFactory.Create(user, []);
        return new ProfileDto(
            user.Id,
            user.FullName,
            user.Username,
            user.Email,
            user.PhoneNumber,
            user.WhatsappNumber,
            userDto.AvatarUrl,
            NextChangeAt(user.FullNameChangedAt, ProfilePolicy.FullNameChangeInterval),
            NextChangeAt(user.UsernameChangedAt, ProfilePolicy.UsernameChangeInterval),
            NextChangeAt(user.AvatarChangedAt, ProfilePolicy.AvatarChangeInterval));
    }

    private static TimeZoneInfo ResolveWib()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("WIB", TimeSpan.FromHours(7), "WIB", "WIB");
        }
    }
}
