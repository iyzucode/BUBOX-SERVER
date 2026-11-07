using System.Security.Claims;
using Bubox.Application.DTOs.Biodata;
using Bubox.Application.DTOs.Profile;
using Bubox.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BiodataController(IAddressService addressService, IProfileService profileService) : ControllerBase
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

    /// <summary>Data screen Detail Profil, termasuk kapan nama/username/foto boleh diubah lagi.</summary>
    [HttpGet("Profile")]
    public async Task<IActionResult> GetProfile()
    {
        return Ok(await profileService.GetProfileAsync(GetUserId()));
    }

    /// <summary>Ganti nama lengkap (7 hari sekali).</summary>
    [HttpPut("Profile/Name")]
    public async Task<IActionResult> UpdateFullName([FromBody] UpdateFullNameRequest request)
    {
        return Ok(await profileService.UpdateFullNameAsync(GetUserId(), request.FullName));
    }

    /// <summary>Ganti username (30 hari sekali).</summary>
    [HttpPut("Profile/Username")]
    public async Task<IActionResult> UpdateUsername([FromBody] UpdateUsernameRequest request)
    {
        return Ok(await profileService.UpdateUsernameAsync(GetUserId(), request.Username));
    }

    /// <summary>Ganti nomor HP & WhatsApp.</summary>
    [HttpPut("Profile/Contact")]
    public async Task<IActionResult> UpdateContact([FromBody] UpdateContactRequest request)
    {
        return Ok(await profileService.UpdateContactAsync(GetUserId(), request));
    }

    /// <summary>Unggah/ganti foto profil (multipart, field "file"; 7 hari sekali). Mengembalikan data user terbaru.</summary>
    [HttpPut("Avatar")]
    [RequestSizeLimit(AvatarRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarRequestLimitBytes)]
    public async Task<IActionResult> UpdateAvatar(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errorCode = "INVALID_AVATAR", message = "Pilih foto terlebih dahulu." });
        }

        var userId = GetUserId();
        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream);

        var user = await profileService.UpdateAvatarAsync(userId, stream.ToArray());
        return Ok(user);
    }

    [HttpDelete("Avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await profileService.RemoveAvatarAsync(GetUserId());
        return Ok(user);
    }

    /// <summary>
    /// Gambar foto profil. Publik agar bisa dipakai langsung di &lt;img&gt;; URL dari API
    /// sudah berversi (?v=…) sehingga aman di-cache lama.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("Avatar/{userId:guid}")]
    public async Task<IActionResult> GetAvatar(Guid userId)
    {
        var avatar = await profileService.GetAvatarAsync(userId);
        if (avatar is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(avatar.Data, avatar.ContentType);
    }

    // Batas body sedikit di atas batas foto (2 MB) untuk overhead multipart.
    private const long AvatarRequestLimitBytes = 3 * 1024 * 1024;
}
