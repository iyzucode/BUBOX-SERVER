namespace Bubox.Application.DTOs.Profile;

/// <summary>
/// Data screen Detail Profil. *ChangeAvailableAt = kapan boleh diubah lagi (UTC);
/// null = boleh diubah sekarang.
/// </summary>
public record ProfileDto(
    Guid Id,
    string FullName,
    string Username,
    string Email,
    string? PhoneNumber,
    string? WhatsappNumber,
    string? AvatarUrl,
    DateTime? FullNameChangeAvailableAt,
    DateTime? UsernameChangeAvailableAt,
    DateTime? AvatarChangeAvailableAt);

public record UpdateFullNameRequest(string FullName);
public record UpdateUsernameRequest(string Username);

/// <summary>Kosongkan (null/"") untuk menghapus nomor.</summary>
public record UpdateContactRequest(string? PhoneNumber, string? WhatsappNumber);
