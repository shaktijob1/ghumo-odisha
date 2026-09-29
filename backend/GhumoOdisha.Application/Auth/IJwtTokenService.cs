using GhumoOdisha.Domain.Entities;

namespace GhumoOdisha.Application.Auth;

public interface IJwtTokenService
{
    string GenerateCustomerToken(Customer customer);

    string GenerateAdminToken(AdminUser adminUser);

    /// <summary>"Driver" role; same lifetime as a customer token (drivers also get refresh tokens).</summary>
    string GenerateDriverToken(Driver driver);
}
