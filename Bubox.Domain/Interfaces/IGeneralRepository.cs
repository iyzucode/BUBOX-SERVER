using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IGeneralRepository
{
    Task<IEnumerable<RegionItem>> GetProvincesAsync();
    Task<IEnumerable<RegionItem>> GetCitiesAsync(string provinceCode);
    Task<IEnumerable<RegionItem>> GetDistrictsAsync(string cityCode);
    Task<IEnumerable<VillageItem>> GetVillagesAsync(string districtCode);
}
