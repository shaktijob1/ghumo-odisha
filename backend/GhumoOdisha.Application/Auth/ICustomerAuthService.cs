using GhumoOdisha.Application.Auth.Dtos;

namespace GhumoOdisha.Application.Auth;

public interface ICustomerAuthService
{
    Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default);

    Task<CustomerAuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

    Task<CustomerAuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
