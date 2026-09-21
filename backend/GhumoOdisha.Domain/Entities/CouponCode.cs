namespace GhumoOdisha.Domain.Entities;

public class CouponCode
{
    public int CouponCodeId { get; set; }
    public string Code { get; set; } = null!;
    public decimal DiscountAmount { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CouponRedemption> Redemptions { get; set; } = new List<CouponRedemption>();
}
