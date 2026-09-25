using Bubox.Application.DTOs.Menu;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController(IMenuService menuService) : ControllerBase
{
    [HttpGet("GetWeeklyMenu")]
    public async Task<IActionResult> GetWeeklyMenu()
    {
        var result = await menuService.GetWeeklyMenuScheduleAsync();
        return Ok(result);
    }

    [HttpGet("GetMenuByDay")]
    public async Task<IActionResult> GetMenuByDay([FromQuery] int dayOfWeek)
    {
        if (dayOfWeek < 1 || dayOfWeek > 7)
        {
            return BadRequest(new { message = "Parameter 'dayOfWeek' harus bernilai antara 1 (Senin) hingga 7 (Minggu)." });
        }

        try
        {
            var result = await menuService.GetMenuByDayAsync(dayOfWeek);
            return Ok(result);
        }
        catch (InvalidMenuDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetDetailMenu")]
    public async Task<IActionResult> GetDetailMenu([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' menu wajib disertakan." });
        }

        var menu = await menuService.GetMenuByIdAsync(id);
        if (menu == null)
        {
            return NotFound(new { message = "Menu tidak ditemukan." });
        }

        return Ok(menu);
    }

    // [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}")] // Dinonaktifkan sementara untuk masa development
    [HttpPost("CreateMenu")]
    public async Task<IActionResult> CreateMenu([FromBody] CreateMenuRequest request)
    {
        try
        {
            var menuId = await menuService.CreateMenuAsync(request);
            return Ok(new { id = menuId, message = "Menu berhasil ditambahkan." });
        }
        catch (InvalidMenuDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat membuat menu.", detail = ex.Message });
        }
    }

    // [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}")] // Dinonaktifkan sementara untuk masa development
    [HttpPut("UpdateMenu")]
    public async Task<IActionResult> UpdateMenu([FromQuery] Guid id, [FromBody] UpdateMenuRequest request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' menu wajib disertakan." });
        }

        try
        {
            await menuService.UpdateMenuAsync(id, request);
            return Ok(new { id, message = "Menu berhasil diperbarui." });
        }
        catch (MenuNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidMenuDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat memperbarui menu.", detail = ex.Message });
        }
    }

    // [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}")] // Dinonaktifkan sementara untuk masa development
    [HttpDelete("DeleteMenu")]
    public async Task<IActionResult> DeleteMenu([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' menu wajib disertakan." });
        }

        try
        {
            await menuService.DeleteMenuAsync(id);
            return Ok(new { message = "Menu berhasil dihapus." });
        }
        catch (MenuNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat menghapus menu.", detail = ex.Message });
        }
    }

    // [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}")] // Dinonaktifkan sementara untuk masa development
    [HttpPut("ToggleMenuStatus")]
    public async Task<IActionResult> ToggleMenuStatus([FromQuery] Guid id, [FromQuery] bool isActive)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' menu wajib disertakan." });
        }

        try
        {
            await menuService.ToggleStatusAsync(id, isActive);
            return Ok(new { id, isActive, message = $"Status menu berhasil diubah menjadi {(isActive ? "Aktif" : "Non-Aktif")}." });
        }
        catch (MenuNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat mengubah status menu.", detail = ex.Message });
        }
    }
}
