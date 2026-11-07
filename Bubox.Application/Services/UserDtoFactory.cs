using Bubox.Application.DTOs.Auth;
using Bubox.Domain.Entities;

namespace Bubox.Application.Services;

public static class UserDtoFactory
{
    /// <summary>Path endpoint foto profil; dipakai juga oleh controller.</summary>
    public const string AvatarPathPrefix = "/api/Biodata/Avatar/";

    public static UserDto Create(User user, List<string> roles) =>
        new(user.Id, user.Username, user.Email, user.FullName, user.IsEmailVerified, roles, BuildAvatarUrl(user));

    /// <summary>
    /// URL berversi (?v=…) supaya gambar boleh di-cache lama oleh browser/CDN
    /// dan otomatis berganti saat foto diperbarui.
    /// </summary>
    private static string? BuildAvatarUrl(User user) =>
        user.AvatarUpdatedAt is { } updatedAt
            ? $"{AvatarPathPrefix}{user.Id}?v={updatedAt.ToUniversalTime().Ticks}"
            : null;
}
