using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GhumoOdisha.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static int GetCustomerId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Token is missing the subject claim.");

        return int.Parse(value);
    }
}
