namespace GhumoOdisha.Domain.Entities;

public class CouponCode
{
    public int CouponCodeId { get; set; }
    public string Code { get; set; } = null!;

    /// <summary>The person the coupon was issued to (an agent / referrer) — commission is paid to them.</summary>
    public string HolderName { get; set; } = null!;
    public decimal DiscountAmount { get; set; }
    public decimal CommissionPerSeat { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public bool IsActive { get; set; }
    public bool IsFirstTimeCustomerOnly { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CouponRedemption> Redemptions { get; set; } = new List<CouponRedemption>();
}
