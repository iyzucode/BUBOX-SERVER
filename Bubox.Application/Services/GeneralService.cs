using Bubox.Application.DTOs.General;
using Bubox.Application.Interfaces;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class GeneralService(IGeneralRepository generalRepository) : IGeneralService
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
}
