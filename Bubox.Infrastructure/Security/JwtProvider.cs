using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bubox.Application.Interfaces;
using Bubox.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Bubox.Infrastructure.Security;

public class JwtProvider(IConfiguration configuration) : IJwtProvider
{
    public string GenerateToken(User user, IEnumerable<string> roles)
    {
        var key = configuration["Jwt:Key"]
            ?? "DefaultFallbackSuperSecretKeyBubox2026!MustBeAtLeast32Chars";
        var issuer = configuration["Jwt:Issuer"] ?? "Bubox.Api";
        var audience = configuration["Jwt:Audience"] ?? "Bubox.Client";
        var expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 1440; // 1 day default

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new("username", user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
