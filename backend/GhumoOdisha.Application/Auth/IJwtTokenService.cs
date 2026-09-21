using GhumoOdisha.Domain.Entities;

namespace GhumoOdisha.Application.Auth;

public interface IJwtTokenService
{
    string GenerateCustomerToken(Customer customer);

    string GenerateAdminToken(AdminUser adminUser);
}
