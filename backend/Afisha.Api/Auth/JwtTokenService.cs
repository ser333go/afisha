using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Afisha.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Afisha.Api.Auth;

/// <summary>Выдаёт JWT. Ключ подписи берётся из конфигурации (user-secrets), а не из кода.</summary>
public sealed class JwtTokenService(IConfiguration configuration)
{
    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key не задан. Выполните: dotnet user-secrets set \"Jwt:Key\" \"<случайная строка длиной не менее 32 символов>\".");

        var issuer = configuration["Jwt:Issuer"] ?? "Afisha.Api";
        var audience = configuration["Jwt:Audience"] ?? "Afisha.Client";
        var lifetimeHours = configuration.GetValue("Jwt:LifetimeHours", 12);
        var expiresAt = DateTime.UtcNow.AddHours(lifetimeHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
