using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Afisha.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Достаёт id пользователя из claim "sub" выданного JWT.</summary>
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}
