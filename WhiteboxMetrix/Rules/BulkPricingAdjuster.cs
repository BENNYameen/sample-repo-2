using WhiteboxMetrix.Models;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Rules;

/// <summary>V2: bulk pricing — separate type so coverage drops when added without tests.</summary>
public static class BulkPricingAdjuster
{
    public static decimal AdjustLineTotal(decimal lineSubtotal, int quantity, Product product)
    {
        if (quantity <= 0)
            return 0m;

        if (product.BulkThresholdUnits <= 0)
            return lineSubtotal;

        if (quantity > product.BulkThresholdUnits)
        {
            var rate = product.BulkDiscountPercent / 100m;
            if (quantity >= product.BulkThresholdUnits * 2)
                rate += 0.01m;
            var discount = MoneyUtils.ChainDiscount(lineSubtotal, rate);
            return lineSubtotal - discount;
        }

        if (quantity == product.BulkThresholdUnits)
        {
            var rate = product.BulkDiscountPercent / 100m;
            return lineSubtotal - MoneyUtils.ChainDiscount(lineSubtotal, rate * 0.9m);
        }

        return lineSubtotal;
    }
}
