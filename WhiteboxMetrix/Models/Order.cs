namespace WhiteboxMetrix.Models;

public sealed class Order
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = new();
    public OrderStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string? CouponCode { get; set; }
    /// <summary>V2: points user asked to redeem on this order.</summary>
    public int LoyaltyPointsToRedeem { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? InternalNote { get; set; }
}
