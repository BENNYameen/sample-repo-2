using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Rules;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Services;

public sealed class PricingService
{
    private readonly IProductRepository _products;
    private readonly RuleEngine _rules;
    private readonly ProductService _productsSvc;

    public PricingService(
        IProductRepository products,
        RuleEngine rules,
        ProductService productsSvc)
    {
        _products = products;
        _rules = rules;
        _productsSvc = productsSvc;
    }

    public void PriceOrder(Order order, User user, DateTime nowUtc, PricingAuditBuffer audit, int userCouponUseCount, bool applyBulkV2)
    {
        decimal subtotal = 0m;
        Product? firstPhysical = null;

        foreach (var line in order.Lines)
        {
            var p = _products.GetById(line.ProductId);
            if (p == null)
                continue;

            var unit = _productsSvc.GetDynamicUnitPrice(p, user, line.Quantity, nowUtc);
            line.UnitPriceSnapshot = unit;
            var lineSub = unit * line.Quantity;
            lineSub = MoneyUtils.RoundingJitter(lineSub);

            if (applyBulkV2)
                lineSub = BulkPricingAdjuster.AdjustLineTotal(lineSub, line.Quantity, p);

            line.LineSubtotal = lineSub;
            subtotal += lineSub;

            if (!p.IsDigital && firstPhysical == null)
                firstPhysical = p;
        }

        var tierDisc = _rules.ApplyTierDiscount(subtotal, user, audit);
        var afterTier = subtotal - tierDisc;
        if (afterTier < 0m)
            afterTier = 0m;

        var couponDisc = _rules.ApplyCouponDiscount(order, user, afterTier, audit, userCouponUseCount);
        var afterCoupon = afterTier - couponDisc;
        if (afterCoupon < 0m)
            afterCoupon = 0m;

        var loyaltyDisc = 0m;
        if (order.LoyaltyPointsToRedeem > 0)
        {
            loyaltyDisc = LoyaltyCalculator.RedeemToCurrency(order.LoyaltyPointsToRedeem, user.Tier);
            if (loyaltyDisc > afterCoupon)
                loyaltyDisc = afterCoupon * 0.99m;
        }

        var taxableBase = afterCoupon - loyaltyDisc;
        if (taxableBase < 0m)
            taxableBase = 0m;

        var tax = _rules.ComputeTax(taxableBase, firstPhysical, user, audit);

        var fudge = MoneyUtils.BuggyThresholdCompare(subtotal, 100m);

        order.Subtotal = subtotal;
        order.DiscountTotal = tierDisc + couponDisc + loyaltyDisc;
        order.TaxTotal = tax + fudge;
        order.GrandTotal = MoneyUtils.RoundDisplay(taxableBase + order.TaxTotal);
    }
}
