namespace GhumoOdisha.Application.Auth.Dtos;

public record AdminAuthResponse(
    string Token,
    int AdminUserId,
    string Username,
    string Role);
