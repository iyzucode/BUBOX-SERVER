using Bubox.Api.Extensions;
using Bubox.Application.DTOs.Order;
using Bubox.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrderController(IOrderService orderService) : ControllerBase
{
    [HttpPost("CreateOrder")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await orderService.CreateOrderAsync(User.ToOrderActor(), request);
        return Ok(result);
    }

    [HttpGet("GetMyOrders")]
    public async Task<IActionResult> GetMyOrders()
    {
        var result = await orderService.GetMyOrdersAsync(User.GetUserId());
        return Ok(result);
    }

    [HttpGet("GetDetailOrder")]
    public async Task<IActionResult> GetDetailOrder([FromQuery] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' pesanan wajib disertakan." });
        }

        var result = await orderService.GetOrderDetailAsync(id, User.ToOrderActor());
        return Ok(result);
    }

    [HttpPut("CancelOrder")]
    public async Task<IActionResult> CancelOrder([FromQuery] Guid id, [FromBody] CancelOrderRequest? request)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Parameter 'id' pesanan wajib disertakan." });
        }

        await orderService.CancelOrderAsync(id, User.ToOrderActor(), request?.Reason);
        return Ok(new { message = "Pesanan berhasil dibatalkan." });
    }
}
