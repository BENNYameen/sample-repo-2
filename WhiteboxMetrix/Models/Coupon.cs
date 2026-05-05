namespace WhiteboxMetrix.Models;

/// <summary>V2: coupon definitions — loaded in-memory; tests do not exercise most branches.</summary>
public sealed class Coupon
{
    public string Code { get; set; } = string.Empty;
    public decimal PercentOff { get; set; }
    public decimal MinOrderSubtotal { get; set; }
    public int MaxRedemptionsPerUser { get; set; } = 1;
    public bool StackableWithLoyalty { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}
