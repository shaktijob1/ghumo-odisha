namespace GhumoOdisha.Application.Auth.Dtos;

public record RefreshTokenRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);
