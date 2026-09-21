namespace GhumoOdisha.Domain.Entities;

public class CustomerOtp
{
    public int CustomerOtpId { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public string? Name { get; set; }
    public string OtpHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
