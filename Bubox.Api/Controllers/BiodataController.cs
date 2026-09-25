using System.Security.Claims;
using Bubox.Application.DTOs.Biodata;
using Bubox.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BiodataController(IAddressService addressService) : ControllerBase
{
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Sesi pengguna tidak valid.");
        }

        return userId;
    }

    [HttpGet("GetAddresses")]
    public async Task<IActionResult> GetAddresses()
    {
        var userId = GetUserId();
        var addresses = await addressService.GetAddressesAsync(userId);
        return Ok(addresses);
    }

    [HttpGet("GetDetailAddress")]
    public async Task<IActionResult> GetDetailAddress([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' alamat wajib disertakan." });
        }

        var userId = GetUserId();
        var address = await addressService.GetDetailAddressAsync(id, userId);
        return Ok(address);
    }

    [HttpPost("SaveAddress")]
    public async Task<IActionResult> SaveAddress([FromBody] CreateAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label) ||
            string.IsNullOrWhiteSpace(request.RecipientName) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.FullAddress) ||
            string.IsNullOrWhiteSpace(request.Subdistrict) ||
            string.IsNullOrWhiteSpace(request.City) ||
            string.IsNullOrWhiteSpace(request.Province) ||
            string.IsNullOrWhiteSpace(request.PostalCode))
        {
            return BadRequest(new { message = "Semua bidang alamat wajib diisi." });
        }

        var userId = GetUserId();
        var result = await addressService.SaveAddressAsync(userId, request);
        return Ok(result);
    }

    [HttpPut("UpdateAddress")]
    public async Task<IActionResult> UpdateAddress([FromQuery] Guid id, [FromBody] UpdateAddressRequest request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' alamat wajib disertakan." });
        }

        if (string.IsNullOrWhiteSpace(request.Label) ||
            string.IsNullOrWhiteSpace(request.RecipientName) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.FullAddress) ||
            string.IsNullOrWhiteSpace(request.Subdistrict) ||
            string.IsNullOrWhiteSpace(request.City) ||
            string.IsNullOrWhiteSpace(request.Province) ||
            string.IsNullOrWhiteSpace(request.PostalCode))
        {
            return BadRequest(new { message = "Semua bidang alamat wajib diisi." });
        }

        var userId = GetUserId();
        var result = await addressService.UpdateAddressAsync(id, userId, request);
        return Ok(result);
    }

    [HttpPut("SetPrimaryAddress")]
    public async Task<IActionResult> SetPrimaryAddress([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' alamat wajib disertakan." });
        }

        var userId = GetUserId();
        await addressService.SetPrimaryAddressAsync(id, userId);
        return Ok(new { message = "Alamat berhasil ditetapkan sebagai alamat utama." });
    }

    [HttpDelete("DeleteAddress")]
    public async Task<IActionResult> DeleteAddress([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' alamat wajib disertakan." });
        }

        var userId = GetUserId();
        await addressService.DeleteAddressAsync(id, userId);
        return Ok(new { message = "Alamat berhasil dihapus." });
    }
}
