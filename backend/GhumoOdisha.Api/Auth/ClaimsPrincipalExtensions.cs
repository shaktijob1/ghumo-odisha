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

    /// <summary>The signed-in driver's id — only meaningful on [Authorize(Roles = "Driver")] endpoints.</summary>
    public static int GetDriverId(this ClaimsPrincipal user) => user.GetCustomerId();

    /// <summary>The signed-in admin's id — only meaningful on [Authorize(Roles = "Admin")] endpoints.</summary>
    public static int GetAdminId(this ClaimsPrincipal user) => user.GetCustomerId();
}
