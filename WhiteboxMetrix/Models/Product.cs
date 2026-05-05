namespace WhiteboxMetrix.Models;

public sealed class Product
{
    public string Id { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public int StockQuantity { get; set; }
    public decimal WeightKg { get; set; }
    public bool IsDigital { get; set; }
    /// <summary>When quantity purchased in one order exceeds this, bulk tier may apply (v2 wiring).</summary>
    public int BulkThresholdUnits { get; set; } = 10;
    public decimal BulkDiscountPercent { get; set; } = 5m;
}
