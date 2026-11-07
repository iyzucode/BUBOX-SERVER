using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bubox.Infrastructure.Services.Google;

/// <summary>
/// Reverse geocoding lewat Google Geocoding API (bahasa Indonesia).
/// Komponen wilayah Indonesia: level_1 = provinsi, level_2 = kab/kota,
/// level_3 = kecamatan, level_4 = kelurahan/desa.
/// </summary>
public class GoogleReverseGeocoder(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleReverseGeocoder> logger) : IReverseGeocoder
{
    private readonly GoogleMapsOptions _options = options.Value;

    public async Task<GeocodedAddress?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerApiKey))
        {
            logger.LogError("Geocoding gagal: GoogleMaps:ServerApiKey belum dikonfigurasi");
            throw new GeocodingUnavailableException();
        }

        // Titik dibulatkan 4 desimal (±11 m) agar geser kecil tidak memanggil Google lagi.
        var lat = Math.Round(latitude, 4).ToString("F4", CultureInfo.InvariantCulture);
        var lng = Math.Round(longitude, 4).ToString("F4", CultureInfo.InvariantCulture);
        var cacheKey = $"geocode:{lat},{lng}";
        if (cache.TryGetValue(cacheKey, out GeocodedAddress? cached))
        {
            return cached;
        }

        var url = $"{_options.GeocodingUrl}?latlng={lat},{lng}&language=id&region=id&key={Uri.EscapeDataString(_options.ServerApiKey)}";

        GeocodeResponse? response;
        try
        {
            response = await httpClient.GetFromJsonAsync<GeocodeResponse>(url, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "Geocoding gagal menghubungi Google");
            throw new GeocodingUnavailableException();
        }

        switch (response?.Status)
        {
            case "OK":
                break;
            case "ZERO_RESULTS":
                cache.Set(cacheKey, (GeocodedAddress?)null, TimeSpan.FromHours(_options.CacheHours));
                return null;
            default:
                // REQUEST_DENIED / OVER_QUERY_LIMIT / INVALID_REQUEST: masalah konfigurasi atau kuota.
                logger.LogError("Geocoding ditolak Google: {Status} {Message}", response?.Status, response?.ErrorMessage);
                throw new GeocodingUnavailableException();
        }

        var results = response.Results ?? [];
        var first = results.FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        // Komponen wilayah bisa tersebar di beberapa hasil: ambil kemunculan pertama tiap jenis.
        string? Component(string type) => results
            .SelectMany(r => r.AddressComponents ?? [])
            .FirstOrDefault(c => c.Types?.Contains(type) == true)?.LongName;

        var geocoded = new GeocodedAddress(
            first.FormattedAddress ?? string.Empty,
            first.PlaceId,
            Component("administrative_area_level_1"),
            Component("administrative_area_level_2"),
            Component("administrative_area_level_3"),
            Component("administrative_area_level_4"),
            Component("postal_code"));

        cache.Set(cacheKey, geocoded, TimeSpan.FromHours(_options.CacheHours));
        return geocoded;
    }

    private sealed record GeocodeResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("error_message")] string? ErrorMessage,
        [property: JsonPropertyName("results")] List<GeocodeResult>? Results);

    private sealed record GeocodeResult(
        [property: JsonPropertyName("formatted_address")] string? FormattedAddress,
        [property: JsonPropertyName("place_id")] string? PlaceId,
        [property: JsonPropertyName("address_components")] List<AddressComponent>? AddressComponents);

    private sealed record AddressComponent(
        [property: JsonPropertyName("long_name")] string? LongName,
        [property: JsonPropertyName("types")] List<string>? Types);
}
