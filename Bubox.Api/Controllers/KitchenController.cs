using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Kitchen;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

/// <summary>Validasi & rilis daftar masak (admin, H-1) dan papan dapur (hari H).</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class KitchenController(IKitchenService kitchenService) : ControllerBase
{
    private const string Managers = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}";

    [Authorize(Roles = Managers)]
    [HttpGet("GetValidation")]
    public async Task<IActionResult> GetValidation([FromQuery] DateOnly date) =>
        Ok(await kitchenService.GetValidationAsync(date));

    [Authorize(Roles = Managers)]
    [HttpPut("Release")]
    public async Task<IActionResult> Release([FromBody] KitchenDateRequest request) =>
        Ok(await kitchenService.ReleaseAsync(request.Date, User.GetUserId()));

    [Authorize(Roles = Managers)]
    [HttpPut("CancelRelease")]
    public async Task<IActionResult> CancelRelease([FromBody] KitchenDateRequest request)
    {
        await kitchenService.CancelReleaseAsync(request.Date);
        return Ok(new { message = "Rilis dibatalkan. Dapur tidak lagi melihat daftar tanggal ini." });
    }

    /// <summary>Papan dapur; kosong (released = false) bila admin belum merilis tanggal ini.</summary>
    [Authorize(Roles = $"{Managers},{RoleConstants.Owner},{RoleConstants.Kitchen}")]
    [HttpGet("GetBoard")]
    public async Task<IActionResult> GetBoard([FromQuery] DateOnly date) =>
        Ok(await kitchenService.GetBoardAsync(date));

    /// <summary>Simpan paket yang sudah siap (status hari kirim → READY).</summary>
    [Authorize(Roles = $"{Managers},{RoleConstants.Kitchen}")]
    [HttpPut("MarkReady")]
    public async Task<IActionResult> MarkReady([FromBody] KitchenMarkReadyRequest request) =>
        Ok(await kitchenService.MarkReadyAsync(request.Date, request.OrderDayIds, User.ToOrderActor()));
}
