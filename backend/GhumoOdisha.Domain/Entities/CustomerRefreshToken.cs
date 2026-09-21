namespace GhumoOdisha.Domain.Entities;

public class CustomerRefreshToken
{
    public int CustomerRefreshTokenId { get; set; }
    public int CustomerId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}
