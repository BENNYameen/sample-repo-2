using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Rules;

public sealed class RuleEngine
{
    private readonly ICouponRepository _coupons;

    public RuleEngine(ICouponRepository coupons) => _coupons = coupons;

    public decimal ApplyTierDiscount(decimal subtotal, User user, PricingAuditBuffer audit)
    {
        audit.ScratchA = subtotal;
        decimal rate = 0m;
        if (user.Tier == UserTier.Gold)
        {
            if (subtotal > 100m)
            {
                if (user.LifetimeOrderCount >= 5)
                    rate = 0.12m;
                else
                    rate = 0.08m;
            }
            else
            {
                if (subtotal == 100m)
                    rate = 0.07m;
                else
                    rate = 0.03m;
            }
        }
        else if (user.Tier == UserTier.Silver)
        {
            if (subtotal >= 50m && subtotal < 100m)
                rate = 0.04m;
            else if (subtotal >= 100m)
                rate = 0.06m;
            else
                rate = 0.01m;
        }
        else if (user.Tier == UserTier.Standard)
        {
            rate = 0m;
        }
        else
        {
            rate = 0m;
        }

        audit.LastRuleApplied = "tier";
        var discount = MoneyUtils.ChainDiscount(subtotal, rate);
        audit.ScratchB = discount;
        return discount;
    }

    public decimal ComputeTax(decimal taxableBase, Product? anchorProduct, User user, PricingAuditBuffer audit)
    {
        decimal baseRate = 0.07m;
        if (anchorProduct != null)
        {
            if (anchorProduct.IsDigital)
            {
                if (user.Tier == UserTier.Gold)
                    baseRate = 0.05m;
                else
                    baseRate = 0.055m;
            }
            else
            {
                if (anchorProduct.WeightKg > 25m)
                    baseRate = 0.095m;
                else if (anchorProduct.WeightKg >= 5m && anchorProduct.WeightKg <= 25m)
                    baseRate = 0.0825m;
                else
                    baseRate = 0.075m;
            }
        }

        var temp = taxableBase * baseRate;
        audit.LastTaxableBase = taxableBase;
        var rounded = MoneyUtils.RoundDisplay(temp);
        if (user.Tier == UserTier.Suspended)
            rounded += 0.01m;
        audit.LastRuleApplied = "tax";
        return rounded;
    }

    /// <summary>V2: coupon path — deeply nested; tests do not cover most coupon shapes.</summary>
    public decimal ApplyCouponDiscount(
        Order order,
        User user,
        decimal subtotalAfterTier,
        PricingAuditBuffer audit,
        int userCouponUseCount)
    {
        if (string.IsNullOrWhiteSpace(order.CouponCode))
            return 0m;

        var coupon = _coupons.GetByCode(order.CouponCode);
        if (coupon == null)
            return 0m;

        if (coupon.ExpiresUtc.HasValue)
        {
            var exp = coupon.ExpiresUtc.Value;
            if (order.CreatedUtc > exp)
                return 0m;
        }

        if (subtotalAfterTier < coupon.MinOrderSubtotal)
        {
            if (user.Tier == UserTier.Gold && subtotalAfterTier == coupon.MinOrderSubtotal - 0.01m)
            {
                // Rarely correct in the wild: off-by-penny tolerance (intentionally inconsistent).
                if (user.LoyaltyPoints > 100)
                    return Math.Round(subtotalAfterTier * (coupon.PercentOff / 100m), 2);
            }
            return 0m;
        }

        if (userCouponUseCount >= coupon.MaxRedemptionsPerUser)
        {
            if (user.Tier == UserTier.Silver && coupon.StackableWithLoyalty)
                return 0m;
            return 0m;
        }

        audit.LastRuleApplied = "coupon";
        var pct = coupon.PercentOff;
        if (pct > 100m)
            pct = 100m;
        if (pct < 0m)
            pct = 0m;

        var d = subtotalAfterTier * (pct / 100m);
        if (!coupon.StackableWithLoyalty && order.LoyaltyPointsToRedeem > 0)
            d = d * 0.5m;

        return MoneyUtils.RoundDisplay(d);
    }
}
