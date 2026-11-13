using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Delivery;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

/// <summary>Admin: area pengantaran, zona ("driver wilayah"), driver, dan penugasan manual kiriman.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}")]
public class DeliveryAdminController(IDeliveryAdminService service) : ControllerBase
{
    // ---- Area (pohon wilayah) ----

    /// <summary>Anak langsung sebuah wilayah beserta aturan cakupan & zona. Tanpa parentCode = daftar provinsi.</summary>
    [HttpGet("GetAreaChildren")]
    public async Task<IActionResult> GetAreaChildren([FromQuery] string? parentCode) =>
        Ok(await service.GetAreaChildrenAsync(parentCode));

    [HttpGet("SearchWilayah")]
    public async Task<IActionResult> SearchWilayah([FromQuery] string q) =>
        Ok(await service.SearchWilayahAsync(q));

    /// <summary>isCovered true/false = aturan sendiri; null = hapus aturan (ikut induk).</summary>
    [HttpPut("SetCoverage")]
    public async Task<IActionResult> SetCoverage([FromBody] SetCoverageRequest request) =>
        Ok(await service.SetCoverageAsync(request, User.GetUserId()));

    /// <summary>zoneId = petakan ke zona; null = hapus pemetaan (ikut induk).</summary>
    [HttpPut("SetZoneArea")]
    public async Task<IActionResult> SetZoneArea([FromBody] SetZoneAreaRequest request) =>
        Ok(await service.SetZoneAreaAsync(request, User.GetUserId()));

    // ---- Zona ----

    [HttpGet("GetZones")]
    public async Task<IActionResult> GetZones() => Ok(await service.GetZonesAsync());

    [HttpPost("CreateZone")]
    public async Task<IActionResult> CreateZone([FromBody] UpsertZoneRequest request) =>
        Ok(await service.CreateZoneAsync(request, User.GetUserId()));

    [HttpPut("UpdateZone")]
    public async Task<IActionResult> UpdateZone([FromQuery] Guid id, [FromBody] UpsertZoneRequest request) =>
        Ok(await service.UpdateZoneAsync(id, request, User.GetUserId()));

    /// <summary>Ganti driver zona; driverUserId null = kosongkan zona.</summary>
    [HttpPut("SetZoneDriver")]
    public async Task<IActionResult> SetZoneDriver([FromQuery] Guid zoneId, [FromBody] SetZoneDriverRequest request) =>
        Ok(await service.SetZoneDriverAsync(zoneId, request, User.GetUserId()));

    /// <summary>Hapus zona; hanya bila tidak punya wilayah & driver aktif (selain itu 409).</summary>
    [HttpDelete("DeleteZone")]
    public async Task<IActionResult> DeleteZone([FromQuery] Guid id)
    {
        await service.DeleteZoneAsync(id);
        return Ok(new { message = "Zona dihapus." });
    }

    /// <summary>Daftar wilayah milik zona (baca-saja). Pemetaan wilayah → zona hanya lewat SetZoneArea.</summary>
    [HttpGet("GetZoneAreas")]
    public async Task<IActionResult> GetZoneAreas([FromQuery] Guid zoneId) =>
        Ok(await service.GetZoneAreasAsync(zoneId));

    [HttpGet("GetZoneDriverHistory")]
    public async Task<IActionResult> GetZoneDriverHistory([FromQuery] Guid zoneId) =>
        Ok(await service.GetZoneDriverHistoryAsync(zoneId));

    // ---- Driver ----

    [HttpGet("GetDrivers")]
    public async Task<IActionResult> GetDrivers() => Ok(await service.GetDriversAsync());

    [HttpGet("SearchUsers")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q) => Ok(await service.SearchUsersAsync(q));

    [HttpPut("GrantDriver")]
    public async Task<IActionResult> GrantDriver([FromQuery] Guid userId)
    {
        await service.GrantDriverAsync(userId);
        return Ok(new { message = "Peran Driver ditambahkan." });
    }

    [HttpPut("RevokeDriver")]
    public async Task<IActionResult> RevokeDriver([FromQuery] Guid userId)
    {
        await service.RevokeDriverAsync(userId, User.GetUserId());
        return Ok(new { message = "Peran Driver dicabut." });
    }

    // ---- Penugasan manual kiriman ----

    [HttpPut("SetDayDriver")]
    public async Task<IActionResult> SetDayDriver([FromBody] SetDayDriverRequest request) =>
        Ok(await service.SetDayDriverAsync(request, User.GetUserId()));
}
