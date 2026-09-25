using Bubox.Application.DTOs.General;

namespace Bubox.Application.Interfaces;

public interface IGeneralService
{
    Task<IEnumerable<ProvinceDto>> GetProvincesAsync();
    Task<IEnumerable<CityDto>> GetCitiesAsync(string provinceCode);
    Task<IEnumerable<DistrictDto>> GetDistrictsAsync(string cityCode);
    Task<IEnumerable<VillageDto>> GetVillagesAsync(string districtCode);
}
