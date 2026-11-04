using System.Security.Claims;
using Bubox.Application.Services;

namespace Bubox.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Sesi pengguna tidak valid.");
        }

        return userId;
    }

    public static OrderActor ToOrderActor(this ClaimsPrincipal user) =>
        OrderActor.FromUser(user.GetUserId(), user.FindAll(ClaimTypes.Role).Select(c => c.Value));
}
