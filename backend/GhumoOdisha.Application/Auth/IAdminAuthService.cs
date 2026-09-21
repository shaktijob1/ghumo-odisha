using GhumoOdisha.Application.Auth.Dtos;

namespace GhumoOdisha.Application.Auth;

public interface IAdminAuthService
{
    Task<AdminAuthResponse> LoginAsync(AdminLoginRequest request, CancellationToken cancellationToken = default);
}
