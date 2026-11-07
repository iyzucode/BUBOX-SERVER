namespace Bubox.Domain.Constants;

/// <summary>Batas seberapa sering data profil boleh diubah.</summary>
public static class ProfilePolicy
{
    public static readonly TimeSpan FullNameChangeInterval = TimeSpan.FromDays(7);
    public static readonly TimeSpan UsernameChangeInterval = TimeSpan.FromDays(30);
    public static readonly TimeSpan AvatarChangeInterval = TimeSpan.FromDays(7);
}

/// <summary>Hasil ganti username (cek batas waktu & keunikan dilakukan atomik di database).</summary>
public enum UsernameUpdateResult
{
    Updated,
    CooldownActive,
    Taken,
}
