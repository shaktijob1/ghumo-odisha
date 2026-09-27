using GhumoOdisha.Application.Auth.Dtos;

namespace GhumoOdisha.Application.Auth;

/// <summary>Customer sign-in: WhatsApp OTP or Google only.</summary>
public interface ICustomerAuthService
{
    Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default);

    Task<CustomerAuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

    Task<CustomerAuthResponse> GoogleSignInAsync(GoogleSignInRequest request, CancellationToken cancellationToken = default);

    Task<RequestOtpResponse> RequestAddPhoneOtpAsync(int customerId, AddPhoneRequest request, CancellationToken cancellationToken = default);

    Task VerifyAddPhoneOtpAsync(int customerId, VerifyOtpRequest request, CancellationToken cancellationToken = default);

    Task LinkGoogleAsync(int customerId, GoogleSignInRequest request, CancellationToken cancellationToken = default);

    Task<CustomerAuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
