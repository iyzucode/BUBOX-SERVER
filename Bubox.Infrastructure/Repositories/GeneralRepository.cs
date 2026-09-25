using Dapper;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;

namespace Bubox.Infrastructure.Repositories;

public class GeneralRepository(IDbConnectionFactory connectionFactory) : IGeneralRepository
{
    public async Task<IEnumerable<RegionItem>> GetProvincesAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT kode AS Code, nama AS Name
            FROM rf_wilayah
            WHERE LENGTH(kode) = 2
            ORDER BY nama ASC;
            """;

        return await connection.QueryAsync<RegionItem>(sql);
    }

    public async Task<IEnumerable<RegionItem>> GetCitiesAsync(string provinceCode)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT kode AS Code, nama AS Name
            FROM rf_wilayah
            WHERE LENGTH(kode) = 5 AND kode LIKE @Prefix
            ORDER BY nama ASC;
            """;

        var prefix = provinceCode.Trim() + ".%";
        return await connection.QueryAsync<RegionItem>(sql, new { Prefix = prefix });
    }

    public async Task<IEnumerable<RegionItem>> GetDistrictsAsync(string cityCode)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT kode AS Code, nama AS Name
            FROM rf_wilayah
            WHERE LENGTH(kode) = 8 AND kode LIKE @Prefix
            ORDER BY nama ASC;
            """;

        var prefix = cityCode.Trim() + ".%";
        return await connection.QueryAsync<RegionItem>(sql, new { Prefix = prefix });
    }

    public async Task<IEnumerable<VillageItem>> GetVillagesAsync(string districtCode)
    {
        using var connection = connectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                w.kode AS Code, 
                w.nama AS Name, 
                k.kodepos AS PostalCode
            FROM rf_wilayah w
            LEFT JOIN rf_kodepos k ON w.kode = k.kode
            WHERE LENGTH(w.kode) = 13 AND w.kode LIKE @Prefix
            ORDER BY w.nama ASC;
            """;

        var prefix = districtCode.Trim() + ".%";
        return await connection.QueryAsync<VillageItem>(sql, new { Prefix = prefix });
    }
}
