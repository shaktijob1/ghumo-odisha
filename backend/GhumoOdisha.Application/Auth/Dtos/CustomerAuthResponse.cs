namespace GhumoOdisha.Application.Auth.Dtos;

public record CustomerAuthResponse(
    string Token,
    string RefreshToken,
    int CustomerId,
    string Name,
    string PhoneNumber,
    string? Email);
