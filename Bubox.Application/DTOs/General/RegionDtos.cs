namespace Bubox.Application.DTOs.General;

public record ProvinceDto(string Code, string Name);
public record CityDto(string Code, string Name);
public record DistrictDto(string Code, string Name);
public record VillageDto(string Code, string Name, string? PostalCode);

/// <summary>Wilayah master (rf_wilayah) yang cocok dengan hasil peta.</summary>
public record RegionMatchDto(string Code, string Name);

/// <summary>
/// Hasil pencocokan titik peta ke alamat + master wilayah.
/// Wilayah yang tidak berhasil dicocokkan bernilai null; pengguna memilihnya manual di form.
/// </summary>
public record ResolvedLocationDto(
    double Latitude,
    double Longitude,
    string FormattedAddress,
    string? PlaceId,
    RegionMatchDto? Province,
    RegionMatchDto? City,
    RegionMatchDto? District,
    RegionMatchDto? Village,
    string? PostalCode,
    bool IsFullyMatched);
