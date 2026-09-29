namespace GhumoOdisha.Domain.Entities;

public class DriverRefreshToken
{
    public int DriverRefreshTokenId { get; set; }
    public int DriverId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Driver Driver { get; set; } = null!;
}
