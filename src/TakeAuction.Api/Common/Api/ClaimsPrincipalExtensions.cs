using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TakeAuction.Api.Common.Api;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}
