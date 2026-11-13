using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Vouchers;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bubox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VoucherController(IVoucherService voucherService) : ControllerBase
{
    private const string Managers = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}";

    // ==========================================
    // Pelanggan
    // ==========================================

    /// <summary>Voucher yang tampil di daftar beserta kelayakannya untuk subtotal keranjang.</summary>
    [HttpGet("GetAvailable")]
    public async Task<IActionResult> GetAvailable([FromQuery] decimal subtotal, [FromQuery] string? paymentMethod)
    {
        var result = await voucherService.GetAvailableAsync(User.GetUserId(), subtotal, paymentMethod);
        return Ok(result);
    }

    /// <summary>Cek kode yang diketik (termasuk voucher rahasia). Dibatasi agar kode tidak bisa ditebak.</summary>
    [EnableRateLimiting(RateLimitPolicies.VoucherCheck)]
    [HttpPost("Check")]
    public async Task<IActionResult> Check([FromBody] CheckVoucherRequest request)
    {
        var result = await voucherService.CheckAsync(User.GetUserId(), request);
        return Ok(result);
    }

    // ==========================================
    // Admin
    // ==========================================

    [Authorize(Roles = Managers)]
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        var result = await voucherService.GetAllAsync();
        return Ok(result);
    }

    [Authorize(Roles = Managers)]
    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] UpsertVoucherRequest request)
    {
        var result = await voucherService.CreateAsync(request, User.GetUserId());
        return Ok(result);
    }

    [Authorize(Roles = Managers)]
    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromQuery] Guid id, [FromBody] UpsertVoucherRequest request)
    {
        var result = await voucherService.UpdateAsync(id, request, User.GetUserId());
        return Ok(result);
    }

    [Authorize(Roles = Managers)]
    [HttpPut("SetActive")]
    public async Task<IActionResult> SetActive([FromQuery] Guid id, [FromBody] SetVoucherActiveRequest request)
    {
        var result = await voucherService.SetActiveAsync(id, request.IsActive, User.GetUserId());
        return Ok(result);
    }
}
