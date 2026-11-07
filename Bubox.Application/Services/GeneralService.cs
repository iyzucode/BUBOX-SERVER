using Bubox.Application.DTOs.General;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class GeneralService(IGeneralRepository generalRepository, IReverseGeocoder reverseGeocoder) : IGeneralService
{
    public async Task<IEnumerable<ProvinceDto>> GetProvincesAsync()
    {
        var items = await generalRepository.GetProvincesAsync();
        return items.Select(x => new ProvinceDto(x.Code, x.Name));
    }

    public async Task<IEnumerable<CityDto>> GetCitiesAsync(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return [];
        }

        var items = await generalRepository.GetCitiesAsync(provinceCode.Trim());
        return items.Select(x => new CityDto(x.Code, x.Name));
    }

    public async Task<IEnumerable<DistrictDto>> GetDistrictsAsync(string cityCode)
    {
        if (string.IsNullOrWhiteSpace(cityCode))
        {
            return [];
        }

        var items = await generalRepository.GetDistrictsAsync(cityCode.Trim());
        return items.Select(x => new DistrictDto(x.Code, x.Name));
    }

    public async Task<IEnumerable<VillageDto>> GetVillagesAsync(string districtCode)
    {
        if (string.IsNullOrWhiteSpace(districtCode))
        {
            return [];
        }

        var items = await generalRepository.GetVillagesAsync(districtCode.Trim());
        return items.Select(x => new VillageDto(x.Code, x.Name, x.PostalCode));
    }

    public async Task<ResolvedLocationDto> ResolveLocationAsync(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsNaN(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new InvalidCoordinatesException();
        }

        var geocoded = await reverseGeocoder.ReverseGeocodeAsync(latitude, longitude)
            ?? throw new LocationNotFoundException();

        // Cocokkan bertingkat: provinsi → kab/kota → kecamatan → kelurahan/desa.
        var province = WilayahNameMatcher.FindBest(geocoded.Province, await generalRepository.GetProvincesAsync(), x => x.Name);
        var city = province is null ? null
            : WilayahNameMatcher.FindBest(geocoded.City, await generalRepository.GetCitiesAsync(province.Code), x => x.Name);
        var district = city is null ? null
            : WilayahNameMatcher.FindBest(geocoded.District, await generalRepository.GetDistrictsAsync(city.Code), x => x.Name);
        var village = district is null ? null
            : WilayahNameMatcher.FindBest(geocoded.Village, await generalRepository.GetVillagesAsync(district.Code), x => x.Name);

        // Kode pos dari master (per kelurahan) lebih andal daripada dari peta.
        var postalCode = village?.PostalCode ?? geocoded.PostalCode;

        return new ResolvedLocationDto(
            latitude,
            longitude,
            geocoded.FormattedAddress,
            geocoded.PlaceId,
            province is null ? null : new RegionMatchDto(province.Code, province.Name),
            city is null ? null : new RegionMatchDto(city.Code, city.Name),
            district is null ? null : new RegionMatchDto(district.Code, district.Name),
            village is null ? null : new RegionMatchDto(village.Code, village.Name),
            postalCode,
            village is not null);
    }
}
