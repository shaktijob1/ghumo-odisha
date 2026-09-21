namespace GhumoOdisha.Application.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int CustomerAccessTokenLifetimeMinutes { get; set; } = 60;
    public int CustomerRefreshTokenLifetimeDays { get; set; } = 30;
    public int AdminTokenLifetimeHours { get; set; } = 8;
}
