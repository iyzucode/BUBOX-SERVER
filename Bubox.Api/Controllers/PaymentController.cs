using System.Text.Json;
using Bubox.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>
    /// Webhook HTTP Notification Midtrans. Daftarkan URL ini di dashboard Midtrans:
    /// https://&lt;domain&gt;/api/Payment/MidtransNotification
    /// Keaslian dicek lewat signature_key dan konfirmasi ulang ke Status API.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("MidtransNotification")]
    public async Task<IActionResult> MidtransNotification([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        await paymentService.HandleMidtransNotificationAsync(payload.GetRawText(), cancellationToken);
        return Ok(new { message = "OK" });
    }
}
