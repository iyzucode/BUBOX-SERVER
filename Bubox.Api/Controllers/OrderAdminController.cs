using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Order;
using Bubox.Application.Interfaces;
using Bubox.Application.Services.Export;
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

    /// <summary>Ekspor Excel tampilan per hari kirim (semua baris sesuai filter & tab, tanpa paging).</summary>
    [Authorize(Roles = Viewers)]
    [HttpGet("ExportDays")]
    public async Task<IActionResult> ExportDays([FromQuery] AdminOrderBoardRequest request)
    {
        var file = await boardService.ExportDaysAsync(request);
        return File(file.Content, XlsxWriter.ContentType, file.FileName);
    }

    /// <summary>Ekspor transaksi (per pesanan, dengan rincian biaya).</summary>
    [Authorize(Roles = Viewers)]
    [HttpGet("ExportOrders")]
    public async Task<IActionResult> ExportOrders([FromQuery] AdminOrderBoardRequest request)
    {
        var file = await boardService.ExportOrdersAsync(request);
        return File(file.Content, XlsxWriter.ContentType, file.FileName);
    }

    /// <summary>Lembar dapur untuk dicetak: rekap per menu & daftar kiriman lengkap dengan item dan catatan.</summary>
    [Authorize(Roles = $"{Viewers},{RoleConstants.Kitchen},{RoleConstants.Driver}")]
    [HttpGet("GetKitchenSheet")]
    public async Task<IActionResult> GetKitchenSheet([FromQuery] AdminOrderBoardRequest request) =>
        Ok(await boardService.GetKitchenSheetAsync(request));

    /// <summary>Indikator pesanan baru: jumlah pesanan dibuat & dibayar sejak <paramref name="since"/> (UTC).</summary>
    [Authorize(Roles = $"{Viewers},{RoleConstants.Kitchen},{RoleConstants.Driver}")]
    [HttpGet("GetBoardActivity")]
    public async Task<IActionResult> GetBoardActivity([FromQuery] DateTime since) =>
        Ok(await boardService.GetActivityAsync(since.Kind == DateTimeKind.Local ? since.ToUniversalTime() : since));

    /// <summary>Majukan status beberapa hari kirim sekaligus. Role per transisi tetap dicek state machine.</summary>
    [Authorize(Roles = $"{Managers},{RoleConstants.Kitchen},{RoleConstants.Driver}")]
    [HttpPut("BulkUpdateDayStatus")]
    public async Task<IActionResult> BulkUpdateDayStatus([FromBody] BulkDayStatusRequest request) =>
        Ok(await boardService.BulkUpdateDayStatusAsync(request, User.ToOrderActor()));

    /// <summary>Batalkan beberapa hari kirim sekaligus (force majeure, perlu refund manual).</summary>
    [Authorize(Roles = Managers)]
    [HttpPut("BulkCancelDays")]
    public async Task<IActionResult> BulkCancelDays([FromBody] BulkCancelDaysRequest request) =>
        Ok(await boardService.BulkCancelDaysAsync(request, User.ToOrderActor()));

    /// <summary>Tandai refund manual sudah dilakukan untuk satu atau beberapa hari kirim.</summary>
    [Authorize(Roles = Managers)]
    [HttpPut("MarkDaysRefunded")]
    public async Task<IActionResult> MarkDaysRefunded([FromBody] MarkDaysRefundedRequest request) =>
        Ok(await boardService.MarkDaysRefundedAsync(request, User.GetUserId()));

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
