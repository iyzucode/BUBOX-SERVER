using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Outlet;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

/// <summary>Outlet tempat ambil pesanan (PICKUP): pilihan customer di checkout & pengelolaan admin.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutletController(IOutletService outletService) : ControllerBase
{
    private const string Managers = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}";

    // ==========================================
    // Pelanggan
    // ==========================================

    /// <summary>Outlet aktif + outlet terakhir yang dipakai (bila sudah masuk). Checkout bisa dibuka sebelum login.</summary>
    [AllowAnonymous]
    [HttpGet("GetAvailable")]
    public async Task<IActionResult> GetAvailable()
    {
        Guid? userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        return Ok(await outletService.GetAvailableAsync(userId));
    }

    // ==========================================
    // Admin
    // ==========================================

    [Authorize(Roles = Managers)]
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll() => Ok(await outletService.GetAllAsync());

    [Authorize(Roles = Managers)]
    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] UpsertOutletRequest request) =>
        Ok(await outletService.CreateAsync(request, User.GetUserId()));

    [Authorize(Roles = Managers)]
    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromQuery] Guid id, [FromBody] UpsertOutletRequest request) =>
        Ok(await outletService.UpdateAsync(id, request, User.GetUserId()));

    [Authorize(Roles = Managers)]
    [HttpPut("SetActive")]
    public async Task<IActionResult> SetActive([FromQuery] Guid id, [FromBody] SetOutletActiveRequest request) =>
        Ok(await outletService.SetActiveAsync(id, request.IsActive, User.GetUserId()));

    /// <summary>Hanya outlet yang belum pernah dipakai pesanan.</summary>
    [Authorize(Roles = Managers)]
    [HttpDelete("Delete")]
    public async Task<IActionResult> Delete([FromQuery] Guid id)
    {
        await outletService.DeleteAsync(id);
        return Ok(new { deleted = true });
    }
}
