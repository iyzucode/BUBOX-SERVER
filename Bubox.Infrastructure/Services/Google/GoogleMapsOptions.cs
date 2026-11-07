namespace Bubox.Infrastructure.Services.Google;

/// <summary>Section "GoogleMaps" di configuration.yml.</summary>
public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// API key KHUSUS SERVER (dibatasi ke Geocoding API). Rahasia: jangan commit,
    /// isi lewat configuration.Development.yml atau environment variable GoogleMaps__ServerApiKey.
    /// </summary>
    public string ServerApiKey { get; set; } = string.Empty;

    public string GeocodingUrl { get; set; } = "https://maps.googleapis.com/maps/api/geocode/json";

    /// <summary>Lama hasil geocoding disimpan di memori (per titik dibulatkan ±11 m).</summary>
    public int CacheHours { get; set; } = 24;
}
