namespace Bubox.Domain.Entities;

/// <summary>Foto profil pengguna (gambar utuh, sudah divalidasi formatnya).</summary>
public class UserAvatar
{
    public Guid UserId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
