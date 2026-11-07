namespace Bubox.Infrastructure.Services.Google;

/// <summary>Section "Google" di configuration.yml.</summary>
public class GoogleAuthOptions
{
    public const string SectionName = "Google";

    /// <summary>
    /// Client ID OAuth yang boleh menjadi "audience" ID token (Web, lalu Android/iOS).
    /// Client ID bukan rahasia; client secret TIDAK dipakai di alur ini.
    /// </summary>
    public string[] ClientIds { get; set; } = [];
}
