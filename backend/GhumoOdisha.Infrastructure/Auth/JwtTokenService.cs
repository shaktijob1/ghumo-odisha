using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GhumoOdisha.Infrastructure.Auth;

public class JwtTokenService(IOptions<JwtSettings> jwtOptions) : IJwtTokenService
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    public string GenerateCustomerToken(Customer customer)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.CustomerId.ToString()),
            new Claim(ClaimTypes.Role, "Customer"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return GenerateToken(claims, TimeSpan.FromMinutes(_settings.CustomerAccessTokenLifetimeMinutes));
    }

    public string GenerateAdminToken(AdminUser adminUser)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, adminUser.AdminUserId.ToString()),
            new Claim(ClaimTypes.Role, adminUser.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return GenerateToken(claims, TimeSpan.FromHours(_settings.AdminTokenLifetimeHours));
    }

    private string GenerateToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
