using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

/// <summary>
/// Mencari kode wilayah (rf_wilayah) dari nama-nama di alamat, untuk alamat yang belum punya
/// wilayah_code (alamat lama / kelurahan tidak dipilih dari daftar).
///
/// Kolom Subdistrict berisi "Kecamatan, Kelurahan" (dari form alamat) atau satu nama saja.
/// Hasilnya kode PALING SPESIFIK yang berhasil dicocokkan: kelurahan (13), kecamatan (8),
/// kab/kota (5), atau provinsi (2). Aturan cakupan & zona tetap bisa berlaku untuk kode yang
/// kurang spesifik, karena aturan dicari dari kode itu ke atas.
/// </summary>
public class WilayahCodeResolver(IGeneralRepository generalRepository)
{
    public async Task<string?> ResolveAsync(string? province, string? city, string? subdistrict)
    {
        var provinceItem = WilayahNameMatcher.FindBest(province, await generalRepository.GetProvincesAsync(), x => x.Name);
        if (provinceItem is null) return null;

        var cityItem = WilayahNameMatcher.FindBest(city, await generalRepository.GetCitiesAsync(provinceItem.Code), x => x.Name);
        if (cityItem is null) return provinceItem.Code;

        var parts = (subdistrict ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return cityItem.Code;

        // Bagian pertama biasanya kecamatan
        var district = WilayahNameMatcher.FindBest(parts[0], await generalRepository.GetDistrictsAsync(cityItem.Code), x => x.Name);
        if (district is not null)
        {
            if (parts.Length > 1)
            {
                var village = WilayahNameMatcher.FindBest(parts[^1], await generalRepository.GetVillagesAsync(district.Code), x => x.Name);
                if (village is not null) return village.Code;
            }

            return district.Code;
        }

        // Satu nama yang bukan kecamatan: coba sebagai kelurahan di seluruh kab/kota
        var villageInCity = WilayahNameMatcher.FindBest(parts[^1], await generalRepository.GetVillagesAsync(cityItem.Code), x => x.Name);
        return villageInCity?.Code ?? cityItem.Code;
    }
}
