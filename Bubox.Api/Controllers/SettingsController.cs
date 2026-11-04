using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Settings;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(ISettingsService settingsService) : ControllerBase
{
    private const string Managers = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}";

    [Authorize(Roles = Managers)]
    [HttpGet("GetOrderSettings")]
    public async Task<IActionResult> GetOrderSettings()
    {
        var result = await settingsService.GetOrderSettingsAsync();
        return Ok(result);
    }

    [Authorize(Roles = Managers)]
    [HttpPut("UpdateOrderSettings")]
    public async Task<IActionResult> UpdateOrderSettings([FromBody] UpdateOrderSettingsRequest request)
    {
        var result = await settingsService.UpdateOrderSettingsAsync(request, User.GetUserId());
        return Ok(result);
    }

    /// <summary>Dipakai client untuk menonaktifkan tanggal yang sudah lewat cutoff di kalender.</summary>
    [AllowAnonymous]
    [HttpGet("GetOrderCutoffInfo")]
    public async Task<IActionResult> GetOrderCutoffInfo()
    {
        var result = await settingsService.GetOrderCutoffInfoAsync();
        return Ok(result);
    }
}
