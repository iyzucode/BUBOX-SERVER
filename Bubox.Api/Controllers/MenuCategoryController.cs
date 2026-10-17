using Bubox.Application.DTOs.MenuCategory;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuCategoryController(IMenuCategoryService categoryService) : ControllerBase
{
    [HttpGet("GetCategories")]
    public async Task<IActionResult> GetCategories([FromQuery] string? categoryType, [FromQuery] bool includeInactive = false)
    {
        var result = await categoryService.GetCategoriesAsync(categoryType, includeInactive);
        return Ok(result);
    }

    [HttpGet("GetMainCategories")]
    public async Task<IActionResult> GetMainCategories()
    {
        var result = await categoryService.GetMainCategoriesAsync();
        return Ok(result);
    }

    [HttpGet("GetDetailCategory")]
    public async Task<IActionResult> GetDetailCategory([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' kategori wajib disertakan." });
        }

        var category = await categoryService.GetByIdAsync(id);
        if (category == null)
        {
            return NotFound(new { message = "Kategori menu tidak ditemukan." });
        }

        return Ok(category);
    }

    [HttpPost("CreateCategory")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateMenuCategoryRequest request)
    {
        try
        {
            var id = await categoryService.CreateCategoryAsync(request);
            return Ok(new { id, message = "Kategori menu berhasil ditambahkan." });
        }
        catch (InvalidMenuDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat membuat kategori menu.", detail = ex.Message });
        }
    }

    [HttpPut("UpdateCategory")]
    public async Task<IActionResult> UpdateCategory([FromQuery] Guid id, [FromBody] UpdateMenuCategoryRequest request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' kategori wajib disertakan." });
        }

        try
        {
            await categoryService.UpdateCategoryAsync(id, request);
            return Ok(new { id, message = "Kategori menu berhasil diperbarui." });
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
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat memperbarui kategori menu.", detail = ex.Message });
        }
    }

    [HttpDelete("DeleteCategory")]
    public async Task<IActionResult> DeleteCategory([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' kategori wajib disertakan." });
        }

        try
        {
            await categoryService.DeleteCategoryAsync(id);
            return Ok(new { message = "Kategori menu berhasil dihapus." });
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
            return StatusCode(500, new { message = "Terjadi kesalahan internal saat menghapus kategori menu.", detail = ex.Message });
        }
    }
}
