namespace WhiteboxMetrix.Models;

public sealed class OrderLine
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal LineSubtotal { get; set; }
}
