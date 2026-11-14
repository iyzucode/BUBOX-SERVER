using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Order;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrderAdminController(IOrderAdminService orderAdminService, IAdminOrderBoardService boardService) : ControllerBase
{
    private const string Managers = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin}";
    private const string Viewers = $"{RoleConstants.Admin},{RoleConstants.SuperAdmin},{RoleConstants.Owner}";

    /// <summary>Layar Pesanan, tampilan per hari kirim (filter, tab status, ringkasan, rekap dapur).</summary>
    // Kitchen & Driver juga memakai tampilan per hari kirim (dapur, persiapan, keberangkatan)
    [Authorize(Roles = $"{Viewers},{RoleConstants.Kitchen},{RoleConstants.Driver}")]
    [HttpGet("SearchDays")]
    public async Task<IActionResult> SearchDays([FromQuery] AdminOrderBoardRequest request) =>
        Ok(await boardService.SearchDaysAsync(request));

    /// <summary>Layar Pesanan, tampilan per pesanan.</summary>
    [Authorize(Roles = Viewers)]
    [HttpGet("SearchOrders")]
    public async Task<IActionResult> SearchOrders([FromQuery] AdminOrderBoardRequest request) =>
        Ok(await boardService.SearchOrdersAsync(request));

    /// <summary>Detail pesanan untuk panel samping: data pelanggan & penugasan pengantaran per hari.</summary>
    [Authorize(Roles = $"{Viewers},{RoleConstants.Kitchen},{RoleConstants.Driver}")]
    [HttpGet("GetOrderDetail")]
    public async Task<IActionResult> GetOrderDetail([FromQuery] Guid id) =>
        Ok(await boardService.GetOrderDetailAsync(id, User.ToOrderActor()));

    [Authorize(Roles = Viewers)]
    [HttpGet("GetOrders")]
    public async Task<IActionResult> GetOrders([FromQuery] OrderSearchRequest request)
    {
        var result = await orderAdminService.SearchOrdersAsync(request);
        return Ok(result);
    }

    [Authorize(Roles = $"{Viewers},{RoleConstants.Kitchen}")]
    [HttpGet("GetDailyProduction")]
    public async Task<IActionResult> GetDailyProduction([FromQuery] DateOnly date)
    {
        var result = await orderAdminService.GetDailyProductionAsync(date);
        return Ok(result);
    }

    [Authorize(Roles = $"{Viewers},{RoleConstants.Driver}")]
    [HttpGet("GetDailyDeliveries")]
    public async Task<IActionResult> GetDailyDeliveries([FromQuery] DateOnly date)
    {
        var result = await orderAdminService.GetDailyDeliveriesAsync(date);
        return Ok(result);
    }

    /// <summary>Role per transisi dicek lagi oleh state machine (Kitchen: masak, Driver: antar).</summary>
    [Authorize(Roles = $"{Managers},{RoleConstants.Kitchen},{RoleConstants.Driver}")]
    [HttpPut("UpdateDayStatus")]
    public async Task<IActionResult> UpdateDayStatus([FromQuery] Guid id, [FromBody] UpdateDayStatusRequest request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' hari kirim wajib disertakan." });
        }

        await orderAdminService.UpdateDayStatusAsync(id, request.StatusCode, User.ToOrderActor());
        return Ok(new { message = "Status hari kirim berhasil diperbarui." });
    }

    [Authorize(Roles = Managers)]
    [HttpPut("CancelOrderByAdmin")]
    public async Task<IActionResult> CancelOrderByAdmin([FromQuery] Guid id, [FromBody] AdminCancelRequest request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' pesanan wajib disertakan." });
        }

        await orderAdminService.CancelOrderByAdminAsync(id, request.Reason, User.ToOrderActor());
        return Ok(new { message = "Pesanan berhasil dibatalkan. Lakukan refund manual bila pesanan sudah dibayar." });
    }

    [Authorize(Roles = Managers)]
    [HttpPut("CancelDayByAdmin")]
    public async Task<IActionResult> CancelDayByAdmin([FromQuery] Guid id, [FromBody] AdminCancelRequest request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' hari kirim wajib disertakan." });
        }

        await orderAdminService.CancelDayByAdminAsync(id, request.Reason, User.ToOrderActor());
        return Ok(new { message = "Hari kirim dibatalkan (force majeure). Lakukan refund manual." });
    }
}
