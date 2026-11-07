using Bubox.Application.DTOs.Auth;
using Bubox.Application.DTOs.Profile;
using Bubox.Domain.Entities;

namespace Bubox.Application.Interfaces;

public interface IProfileService
{
    Task<ProfileDto> GetProfileAsync(Guid userId);

    /// <summary>Ganti nama lengkap (7 hari sekali).</summary>
    Task<ProfileDto> UpdateFullNameAsync(Guid userId, string fullName);

    /// <summary>Ganti username (30 hari sekali).</summary>
    Task<ProfileDto> UpdateUsernameAsync(Guid userId, string username);

    /// <summary>Ganti nomor HP & WhatsApp (tanpa batas waktu).</summary>
    Task<ProfileDto> UpdateContactAsync(Guid userId, UpdateContactRequest request);

    /// <summary>Simpan/ganti foto profil (7 hari sekali); mengembalikan data user terbaru.</summary>
    Task<UserDto> UpdateAvatarAsync(Guid userId, byte[] data);

    /// <summary>Hapus foto profil; user kembali tampil dengan huruf awal.</summary>
    Task<UserDto> RemoveAvatarAsync(Guid userId);

    Task<UserAvatar?> GetAvatarAsync(Guid userId);
}
