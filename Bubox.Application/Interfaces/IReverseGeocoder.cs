namespace Bubox.Application.Interfaces;

/// <summary>
/// Hasil reverse geocoding (titik peta → alamat) dalam bentuk mentah dari penyedia peta.
/// Nama wilayah belum tentu sama dengan master wilayah Kemendagri.
/// </summary>
public record GeocodedAddress(
    string FormattedAddress,
    string? PlaceId,
    string? Province,
    string? City,
    string? District,
    string? Village,
    string? PostalCode);

public interface IReverseGeocoder
{
    /// <summary>Null bila tidak ada alamat di titik tersebut (mis. di laut).</summary>
    Task<GeocodedAddress?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
