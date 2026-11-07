namespace Bubox.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Null untuk akun yang hanya masuk lewat Google (belum pernah membuat kata sandi).</summary>
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Waktu foto profil terakhir diubah; null = belum punya foto (tampil huruf awal).</summary>
    public DateTime? AvatarUpdatedAt { get; set; }
    /// <summary>Unggahan foto terakhir (dasar batas 7 hari); tetap terisi walau foto dihapus.</summary>
    public DateTime? AvatarChangedAt { get; set; }
    public DateTime? FullNameChangedAt { get; set; }
    public DateTime? UsernameChangedAt { get; set; }
    public string? PhoneNumber { get; set; }
    public string? WhatsappNumber { get; set; }
}
