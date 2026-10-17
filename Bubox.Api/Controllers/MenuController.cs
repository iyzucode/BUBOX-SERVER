using Bubox.Application.DTOs.Menu;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController(IMenuService menuService) : ControllerBase
{
    // ==========================================
    // 1. JADWAL MINGGUAN & HARIAN (PUBLIK & PEMESANAN)
    // ==========================================

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

    // ==========================================
    // 2. KATALOG MASTER MENU (ADMIN)
    // ==========================================

    [HttpGet("GetMasterMenus")]
    public async Task<IActionResult> GetMasterMenus(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false
    )
    {
        var result = await menuService.GetMasterMenusAsync(categoryId, search, includeInactive);
        return Ok(result);
    }

    [HttpGet("GetDetailMasterMenu")]
    public async Task<IActionResult> GetDetailMasterMenu([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' master menu wajib disertakan." });
        }

        var menu = await menuService.GetMasterMenuByIdAsync(id);
        if (menu == null)
        {
            return NotFound(new { message = "Master menu tidak ditemukan." });
        }

        return Ok(menu);
    }

    [HttpPost("CreateMasterMenu")]
    public async Task<IActionResult> CreateMasterMenu([FromBody] CreateMasterMenuRequest request)
    {
        try
        {
            var menuId = await menuService.CreateMasterMenuAsync(request);
            return Ok(new { id = menuId, message = "Master menu berhasil ditambahkan ke katalog." });
        }
        catch (InvalidMenuDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat membuat master menu.", detail = ex.Message });
        }
    }

    [HttpPut("UpdateMasterMenu")]
    public async Task<IActionResult> UpdateMasterMenu([FromQuery] Guid id, [FromBody] UpdateMasterMenuRequest request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' master menu wajib disertakan." });
        }

        try
        {
            await menuService.UpdateMasterMenuAsync(id, request);
            return Ok(new { id, message = "Master menu berhasil diperbarui." });
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
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat memperbarui master menu.", detail = ex.Message });
        }
    }

    [HttpDelete("DeleteMasterMenu")]
    public async Task<IActionResult> DeleteMasterMenu([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' master menu wajib disertakan." });
        }

        try
        {
            await menuService.DeleteMasterMenuAsync(id);
            return Ok(new { message = "Master menu berhasil dihapus." });
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
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat menghapus master menu.", detail = ex.Message });
        }
    }

    [HttpPut("ToggleMenuStatus")]
    public async Task<IActionResult> ToggleMenuStatus([FromQuery] Guid id, [FromQuery] bool isActive)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' master menu wajib disertakan." });
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

    // ==========================================
    // 3. PENUGASAN JADWAL HARIAN (ASSIGNMENTS)
    // ==========================================

    [HttpPost("AssignMenuToDay")]
    public async Task<IActionResult> AssignMenuToDay([FromBody] AssignMenuRequest request)
    {
        try
        {
            await menuService.AssignMenuToDayAsync(request);
            return Ok(new { message = "Menu berhasil ditugaskan ke hari tersebut." });
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
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat menugaskan menu.", detail = ex.Message });
        }
    }

    [HttpPost("BatchAssignMenusToDay")]
    public async Task<IActionResult> BatchAssignMenusToDay([FromBody] BatchAssignMenusRequest request)
    {
        try
        {
            var count = await menuService.BatchAssignMenusToDayAsync(request);
            return Ok(new { count, message = $"{count} menu berhasil ditugaskan ke jadwal hari." });
        }
        catch (InvalidMenuDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat menugaskan batch menu.", detail = ex.Message });
        }
    }

    [HttpDelete("RemoveMenuFromDay")]
    public async Task<IActionResult> RemoveMenuFromDay([FromQuery] Guid assignmentId)
    {
        if (assignmentId == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'assignmentId' wajib disertakan." });
        }

        try
        {
            await menuService.RemoveAssignmentAsync(assignmentId);
            return Ok(new { message = "Menu berhasil dilepaskan dari jadwal hari." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat melepaskan menu dari jadwal.", detail = ex.Message });
        }
    }

    [HttpDelete("RemoveMenuFromDayByDay")]
    public async Task<IActionResult> RemoveMenuFromDayByDay([FromQuery] Guid menuId, [FromQuery] int dayOfWeek)
    {
        if (menuId == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'menuId' wajib disertakan." });
        }

        try
        {
            await menuService.RemoveAssignmentByMenuAndDayAsync(menuId, dayOfWeek);
            return Ok(new { message = "Menu berhasil dilepaskan dari jadwal hari." });
        }
        catch (InvalidMenuDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat melepaskan menu dari jadwal.", detail = ex.Message });
        }
    }
}
