using System.Text.RegularExpressions;
using Bubox.Application.DTOs.Delivery;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public partial class DeliveryAdminService(
    IDeliveryRepository deliveryRepository,
    IUserRepository userRepository) : IDeliveryAdminService
{
    [GeneratedRegex(@"^\d{2}(\.\d{2}(\.\d{2}(\.\d{4})?)?)?$")]
    private static partial Regex WilayahCodePattern();

    [GeneratedRegex("^[A-Z0-9_-]{2,30}$")]
    private static partial Regex ZoneCodePattern();

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCodePattern();

    private static readonly int[] PrefixLengths = [2, 5, 8, 13];

    private static int LevelOf(string code) => code.Length switch { 2 => 1, 5 => 2, 8 => 3, _ => 4 };

    /// <summary>Induk-induk sebuah kode, dari provinsi ke bawah (tidak termasuk kode itu sendiri).</summary>
    private static List<string> AncestorsOf(string code) =>
        PrefixLengths.Where(l => l < code.Length).Select(l => code[..l]).ToList();

    private static string RequireWilayahCode(string? code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (!WilayahCodePattern().IsMatch(trimmed))
        {
            throw new InvalidDeliverySettingException("Kode wilayah tidak valid.");
        }
        return trimmed;
    }

    // ==========================================
    // Pohon wilayah
    // ==========================================

    public async Task<AreaChildrenDto> GetAreaChildrenAsync(string? parentCode)
    {
        string? parent = string.IsNullOrWhiteSpace(parentCode) ? null : RequireWilayahCode(parentCode);
        if (parent is { Length: 13 })
        {
            throw new InvalidDeliverySettingException("Kelurahan/desa tidak punya wilayah turunan.");
        }

        var children = (await deliveryRepository.GetChildrenAsync(parent)).ToList();
        var parentChain = parent == null ? [] : AncestorsOf(parent).Append(parent).ToList();

        var nodes = await BuildNodesAsync(children, parentChain);

        AreaNodeDto? parentNode = null;
        var breadcrumb = new List<AreaCrumbDto>();
        if (parent != null)
        {
            var chainNodes = new List<WilayahNode>();
            foreach (var code in parentChain)
            {
                var node = await deliveryRepository.GetWilayahAsync(code)
                    ?? throw new InvalidDeliverySettingException("Wilayah tidak ditemukan.");
                chainNodes.Add(node);
            }

            breadcrumb = chainNodes.Select(n => new AreaCrumbDto(n.Code, n.Name)).ToList();
            parentNode = (await BuildNodesAsync([chainNodes[^1]], AncestorsOf(parent)))[0];
        }

        return new AreaChildrenDto(parentNode, breadcrumb, nodes);
    }

    public async Task<IReadOnlyList<WilayahSearchItemDto>> SearchWilayahAsync(string query)
    {
        var q = query?.Trim() ?? string.Empty;
        if (q.Length < 3) return [];

        // 5 digit = kode pos → semua kelurahan/desa yang memakai kode pos itu
        var found = PostalCodePattern().IsMatch(q)
            ? (await deliveryRepository.SearchByPostalCodeAsync(q, 200)).ToList()
            : (await deliveryRepository.SearchWilayahAsync(q, 30)).ToList();

        var codes = found.Select(f => f.Code).ToList();
        var paths = await BuildPathsAsync(codes);
        var zones = (await deliveryRepository.GetZoneAreaRulesAsync(codes)).ToDictionary(r => r.WilayahCode);

        return found.Select(f =>
        {
            zones.TryGetValue(f.Code, out var zone);
            return new WilayahSearchItemDto(f.Code, f.Name, LevelOf(f.Code), paths[f.Code], f.PostalCode, zone?.ZoneId, zone?.ZoneName);
        }).ToList();
    }

    /// <summary>Jalur induk "Provinsi › Kota › Kecamatan" untuk tiap kode (tanpa nama wilayah itu sendiri).</summary>
    private async Task<Dictionary<string, string>> BuildPathsAsync(IReadOnlyCollection<string> codes)
    {
        var ancestorCodes = codes.SelectMany(AncestorsOf).Distinct().ToList();
        var names = (await deliveryRepository.GetWilayahByCodesAsync(ancestorCodes)).ToDictionary(n => n.Code, n => n.Name);
        return codes.Distinct().ToDictionary(
            c => c,
            c => string.Join(" › ", AncestorsOf(c).Where(names.ContainsKey).Select(a => names[a])));
    }

    /// <summary>Hitung aturan sendiri & aturan efektif (dari induk) untuk sekumpulan wilayah bersaudara.</summary>
    private async Task<List<AreaNodeDto>> BuildNodesAsync(IReadOnlyList<WilayahNode> nodes, IReadOnlyList<string> sharedAncestors)
    {
        if (nodes.Count == 0) return [];

        var codes = nodes.Select(n => n.Code).ToList();
        var lookup = codes.Concat(sharedAncestors).Distinct().ToList();

        var coverage = (await deliveryRepository.GetCoverageRulesAsync(lookup)).ToDictionary(r => r.WilayahCode);
        var zones = (await deliveryRepository.GetZoneAreaRulesAsync(lookup)).ToDictionary(r => r.WilayahCode);
        var below = (await deliveryRepository.CountDescendantRulesAsync(codes)).ToDictionary(r => r.Code);
        var drivers = (await deliveryRepository.GetZonesAsync()).ToDictionary(z => z.Id, z => z.DriverName);

        // Nama untuk sumber aturan (induk)
        var names = nodes.ToDictionary(n => n.Code, n => n.Name);
        foreach (var code in sharedAncestors.Where(c => !names.ContainsKey(c)))
        {
            var node = await deliveryRepository.GetWilayahAsync(code);
            if (node != null) names[code] = node.Name;
        }

        return nodes.Select(n =>
        {
            // Dari paling spesifik (wilayah ini) ke provinsi
            var chain = sharedAncestors.Append(n.Code).Reverse().ToList();
            var coverageSource = chain.FirstOrDefault(coverage.ContainsKey);
            var zoneSource = chain.FirstOrDefault(zones.ContainsKey);

            var isCovered = coverageSource != null && coverage[coverageSource].IsCovered;
            var zone = zoneSource != null ? zones[zoneSource] : null;
            var driverName = zone != null && drivers.TryGetValue(zone.ZoneId, out var d) ? d : null;
            below.TryGetValue(n.Code, out var counts);

            string? warning = null;
            if (isCovered && zone == null) warning = "Terjangkau tapi belum masuk zona: pesanan akan masuk antrean tanpa driver.";
            else if (isCovered && !zone!.ZoneIsActive) warning = "Zona wilayah ini nonaktif: pesanan akan masuk antrean tanpa driver.";
            else if (isCovered && driverName == null) warning = $"Zona {zone!.ZoneName} belum punya driver.";

            return new AreaNodeDto(
                Code: n.Code,
                Name: n.Name,
                Level: LevelOf(n.Code),
                HasChildren: n.Code.Length < 13,
                CoverageRule: coverage.TryGetValue(n.Code, out var own) ? own.IsCovered : null,
                IsCovered: isCovered,
                CoverageSourceCode: coverageSource,
                CoverageSourceName: coverageSource != null && names.TryGetValue(coverageSource, out var cs) ? cs : null,
                ZoneRuleId: zones.TryGetValue(n.Code, out var ownZone) ? ownZone.ZoneId : null,
                ZoneId: zone?.ZoneId,
                ZoneName: zone?.ZoneName,
                ZoneIsActive: zone?.ZoneIsActive ?? false,
                ZoneSourceCode: zoneSource,
                ZoneSourceName: zoneSource != null && names.TryGetValue(zoneSource, out var zs) ? zs : null,
                DriverName: driverName,
                CoverageRulesBelow: counts?.CoverageRules ?? 0,
                ZoneRulesBelow: counts?.ZoneRules ?? 0,
                Warning: warning);
        }).ToList();
    }

    private async Task<AreaNodeDto> GetNodeAsync(string code)
    {
        var node = await deliveryRepository.GetWilayahAsync(code)
            ?? throw new InvalidDeliverySettingException("Wilayah tidak ditemukan.");
        return (await BuildNodesAsync([node], AncestorsOf(code)))[0];
    }

    public async Task<AreaNodeDto> SetCoverageAsync(SetCoverageRequest request, Guid adminId)
    {
        var code = RequireWilayahCode(request.WilayahCode);
        _ = await deliveryRepository.GetWilayahAsync(code) ?? throw new InvalidDeliverySettingException("Wilayah tidak ditemukan.");
        await deliveryRepository.SetCoverageAsync(code, request.IsCovered, adminId, DateTime.UtcNow);
        return await GetNodeAsync(code);
    }

    public async Task<AreaNodeDto> SetZoneAreaAsync(SetZoneAreaRequest request, Guid adminId)
    {
        var code = RequireWilayahCode(request.WilayahCode);
        _ = await deliveryRepository.GetWilayahAsync(code) ?? throw new InvalidDeliverySettingException("Wilayah tidak ditemukan.");
        if (request.ZoneId is { } zoneId && await deliveryRepository.GetZoneAsync(zoneId) == null)
        {
            throw new DeliveryZoneNotFoundException();
        }

        await deliveryRepository.SetZoneAreaAsync(code, request.ZoneId, adminId, DateTime.UtcNow);
        return await GetNodeAsync(code);
    }

    // ==========================================
    // Zona
    // ==========================================

    public async Task<IReadOnlyList<DeliveryZoneDto>> GetZonesAsync() =>
        (await deliveryRepository.GetZonesAsync()).Select(ToDto).ToList();

    public async Task<DeliveryZoneDto> CreateZoneAsync(UpsertZoneRequest request, Guid adminId)
    {
        var nowUtc = DateTime.UtcNow;
        var zone = new DeliveryZone { Id = Guid.NewGuid(), CreatedAt = nowUtc };
        await ApplyZoneAsync(zone, request, nowUtc);
        await deliveryRepository.CreateZoneAsync(zone, adminId);
        return ToDto((await deliveryRepository.GetZoneAsync(zone.Id))!);
    }

    public async Task<DeliveryZoneDto> UpdateZoneAsync(Guid id, UpsertZoneRequest request, Guid adminId)
    {
        var zone = await deliveryRepository.GetZoneAsync(id) ?? throw new DeliveryZoneNotFoundException();
        await ApplyZoneAsync(zone, request, DateTime.UtcNow);
        await deliveryRepository.UpdateZoneAsync(zone, adminId);
        return ToDto((await deliveryRepository.GetZoneAsync(id))!);
    }

    private async Task ApplyZoneAsync(DeliveryZone zone, UpsertZoneRequest request, DateTime nowUtc)
    {
        var code = string.Concat((request.Code ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        if (!ZoneCodePattern().IsMatch(code))
        {
            throw new InvalidDeliverySettingException("Kode zona 2-30 karakter: huruf, angka, '-' atau '_'.");
        }

        if (await deliveryRepository.ZoneCodeExistsAsync(code, zone.Id))
        {
            throw new DeliveryZoneCodeExistsException();
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > 100)
        {
            throw new InvalidDeliverySettingException("Nama zona wajib diisi, maksimal 100 karakter.");
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > 300)
        {
            throw new InvalidDeliverySettingException("Deskripsi maksimal 300 karakter.");
        }

        zone.Code = code;
        zone.Name = name;
        zone.Description = description;
        zone.IsActive = request.IsActive;
        zone.UpdatedAt = nowUtc;
    }

    public async Task<DeliveryZoneDto> SetZoneDriverAsync(Guid zoneId, SetZoneDriverRequest request, Guid adminId)
    {
        _ = await deliveryRepository.GetZoneAsync(zoneId) ?? throw new DeliveryZoneNotFoundException();
        if (request.DriverUserId is { } driverId && !await deliveryRepository.IsDriverAsync(driverId))
        {
            throw new InvalidDeliverySettingException("User ini belum ber-peran Driver. Tambahkan dulu di menu Driver.");
        }

        await deliveryRepository.SetZoneDriverAsync(zoneId, request.DriverUserId, adminId, DateTime.UtcNow);
        return ToDto((await deliveryRepository.GetZoneAsync(zoneId))!);
    }

    public async Task<IReadOnlyList<ZoneDriverHistoryDto>> GetZoneDriverHistoryAsync(Guid zoneId) =>
        (await deliveryRepository.GetZoneDriverHistoryAsync(zoneId))
            .Select(h => new ZoneDriverHistoryDto(h.DriverUserId, h.DriverName, h.AssignedAt, h.UnassignedAt))
            .ToList();

    public async Task<IReadOnlyList<ZoneAreaDto>> GetZoneAreasAsync(Guid zoneId)
    {
        _ = await deliveryRepository.GetZoneAsync(zoneId) ?? throw new DeliveryZoneNotFoundException();
        var codes = (await deliveryRepository.GetZoneAreaCodesAsync(zoneId)).ToList();
        if (codes.Count == 0) return [];

        var nodes = (await deliveryRepository.GetWilayahByCodesAsync(codes)).ToDictionary(n => n.Code);
        var paths = await BuildPathsAsync(codes);
        // Jangkauan efektif: aturan paling spesifik dari wilayah itu sendiri atau induknya
        var lookup = codes.SelectMany(c => AncestorsOf(c).Append(c)).Distinct().ToList();
        var coverage = (await deliveryRepository.GetCoverageRulesAsync(lookup)).ToDictionary(r => r.WilayahCode, r => r.IsCovered);

        return codes
            .Where(nodes.ContainsKey)
            .Select(c =>
            {
                var rule = AncestorsOf(c).Append(c).LastOrDefault(coverage.ContainsKey);
                return new ZoneAreaDto(c, nodes[c].Name, LevelOf(c), paths[c], rule != null && coverage[rule]);
            })
            // Urut provinsi → kota → kecamatan → kelurahan sesuai jalurnya, agar mudah dibaca
            .OrderBy(a => a.Path).ThenBy(a => a.Level).ThenBy(a => a.Name)
            .ToList();
    }

    public async Task DeleteZoneAsync(Guid zoneId)
    {
        var zone = await deliveryRepository.GetZoneAsync(zoneId) ?? throw new DeliveryZoneNotFoundException();
        if (zone.AreaCount > 0 || zone.DriverUserId != null || !await deliveryRepository.DeleteZoneIfEmptyAsync(zoneId))
        {
            throw new DeliveryZoneNotEmptyException();
        }
    }

    private static DeliveryZoneDto ToDto(DeliveryZone z) => new(
        z.Id, z.Code, z.Name, z.Description, z.IsActive, z.AreaCount, z.DriverUserId, z.DriverName, z.DriverEmail);

    // ==========================================
    // Driver
    // ==========================================

    public async Task<IReadOnlyList<DriverDto>> GetDriversAsync() =>
        (await deliveryRepository.GetDriversAsync())
            .Select(d => new DriverDto(
                d.UserId, d.FullName, d.Email, d.Username, d.PhoneNumber,
                string.IsNullOrEmpty(d.ZoneNames) ? [] : d.ZoneNames.Split(", ").ToList()))
            .ToList();

    public async Task<IReadOnlyList<UserSearchDto>> SearchUsersAsync(string query)
    {
        var q = query?.Trim() ?? string.Empty;
        if (q.Length < 2) return [];
        return (await deliveryRepository.SearchUsersAsync(q, 20))
            .Select(u => new UserSearchDto(u.UserId, u.FullName, u.Email, u.Username, u.IsDriver))
            .ToList();
    }

    public async Task GrantDriverAsync(Guid userId)
    {
        _ = await userRepository.GetByIdAsync(userId) ?? throw new UserNotFoundException();
        await deliveryRepository.GrantDriverRoleAsync(userId);
    }

    public async Task RevokeDriverAsync(Guid userId, Guid adminId)
    {
        _ = await userRepository.GetByIdAsync(userId) ?? throw new UserNotFoundException();
        await deliveryRepository.RevokeDriverRoleAsync(userId, adminId, DateTime.UtcNow);
    }

    // ==========================================
    // Penugasan manual
    // ==========================================

    public async Task<SetDayDriverResponse> SetDayDriverAsync(SetDayDriverRequest request, Guid adminId)
    {
        var ids = (request.OrderDayIds ?? []).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > 500)
        {
            throw new InvalidDeliverySettingException("Pilih 1-500 kiriman.");
        }

        if (request.DriverUserId is { } driverId && !await deliveryRepository.IsDriverAsync(driverId))
        {
            throw new InvalidDeliverySettingException("User ini belum ber-peran Driver.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > 300) reason = reason[..300];

        var updated = await deliveryRepository.SetDayDriverOverrideAsync(
            ids, request.DriverUserId, request.DriverUserId == null ? null : reason, adminId, DateTime.UtcNow);
        return new SetDayDriverResponse(updated);
    }
}
