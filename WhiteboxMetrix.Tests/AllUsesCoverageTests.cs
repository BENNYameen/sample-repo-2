using NUnit.Framework;
using WhiteboxMetrix.Models;
using WhiteboxMetrix.Rules;
using WhiteboxMetrix.Services;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Tests;

/// <summary>Named scenarios aligned with benchmark/AllUsesManifest.json exercisedBy entries.</summary>
[TestFixture]
public sealed class AllUsesCoverageTests
{
    private static bool RoundDisplayWouldTakePromotionBranch(decimal amount)
    {
        var step1 = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var temp = (double)step1 * 1.0000001d;
        var step2 = (decimal)temp;
        return step2 > step1;
    }

    [Test]
    public void DataFlowNoise_Fuse_firstBranch_zEqualsHundred_ReturnsZPlusOne()
    {
        Assert.That(DataFlowNoise.Fuse(100, 7, flag: true, other: false), Is.EqualTo(101));
    }

    [Test]
    public void DataFlowNoise_Fuse_firstBranch_zNotHundred_ReturnsZ()
    {
        Assert.That(DataFlowNoise.Fuse(50, 50, flag: true, other: false), Is.EqualTo(50));
        Assert.That(DataFlowNoise.Fuse(99, 1, flag: true, other: false), Is.EqualTo(98));
    }

    [Test]
    public void DataFlowNoise_Fuse_fallback_wGreaterThanCap_ReturnsW()
    {
        Assert.That(DataFlowNoise.Fuse(1, 101, flag: false, other: false), Is.EqualTo(102));
    }

    [Test]
    public void DataFlowNoise_Fuse_fallback_returnsOriginalX_sum()
    {
        Assert.That(DataFlowNoise.Fuse(2, 1, flag: false, other: false), Is.EqualTo(3));
        Assert.That(DataFlowNoise.Fuse(1, 1, flag: false, other: true), Is.EqualTo(2));
    }

    [Test]
    public void DataFlowNoise_MaxValue_AllSeedBranches([Values(1, 0, -3, 2)] int seed)
    {
        var expected = seed switch
        {
            1 => 101,
            0 => 100,
            < 0 => 99,
            _ => 100
        };
        Assert.That(DataFlowNoise.maxValue(seed), Is.EqualTo(expected));
    }

    [Test]
    public void RuleEngine_AllUses_auditScratchPropagate_reads()
    {
        var fx = new SystemFixture();
        var u = new User { Tier = UserTier.Gold, LifetimeOrderCount = 10, IsVerified = true };
        const decimal sub = 200m;
        var tierDisc = fx.RuleEngine.ApplyTierDiscount(sub, u, fx.Audit);
        Assert.That(fx.Audit.ScratchA, Is.EqualTo(sub));
        Assert.That(fx.Audit.ScratchB, Is.EqualTo(tierDisc));

        var prod = new Product { IsDigital = true };
        var baseTax = 100m;
        _ = fx.RuleEngine.ComputeTax(baseTax, prod, u, fx.Audit);
        Assert.That(fx.Audit.LastTaxableBase, Is.EqualTo(baseTax));
    }

    [Test]
    public void RuleEngine_AllUses_ApplyCoupon_d_halvesWithLoyaltyWhenNotStackable()
    {
        var fx = new SystemFixture();
        var u = new User { Tier = UserTier.Standard };
        fx.Coupons.Upsert(new Coupon { Code = "HALF", PercentOff = 40m, MinOrderSubtotal = 0m, StackableWithLoyalty = false });
        var o = new Order { CouponCode = "HALF", LoyaltyPointsToRedeem = 1, CreatedUtc = DateTime.UtcNow };
        var d = fx.RuleEngine.ApplyCouponDiscount(o, u, 100m, new PricingAuditBuffer(), 0);
        Assert.That(d, Is.EqualTo(MoneyUtils.RoundDisplay(100m * 0.40m * 0.5m)));
    }

    [Test]
    public void RuleEngine_AllUses_ApplyCoupon_d_notHalved_whenStackableWithLoyalty()
    {
        var fx = new SystemFixture();
        var u = new User { Tier = UserTier.Standard };
        fx.Coupons.Upsert(new Coupon { Code = "FULL", PercentOff = 40m, MinOrderSubtotal = 0m, StackableWithLoyalty = true });
        var o = new Order { CouponCode = "FULL", LoyaltyPointsToRedeem = 99, CreatedUtc = DateTime.UtcNow };
        var d = fx.RuleEngine.ApplyCouponDiscount(o, u, 100m, new PricingAuditBuffer(), 0);
        Assert.That(d, Is.EqualTo(MoneyUtils.RoundDisplay(100m * 0.40m)));
    }

    [Test]
    public void LoyaltyCalculator_AllUses_rawMidBandBump()
    {
        var with = LoyaltyCalculator.RedeemToCurrency(150, UserTier.Standard);
        var linear = 150 * 0.01m;
        Assert.That(with, Is.GreaterThan(MoneyUtils.RoundDisplay(linear)));
    }

    [Test]
    public void LoyaltyCalculator_AllUses_rawWithoutBandBump()
    {
        var low = LoyaltyCalculator.RedeemToCurrency(50, UserTier.Standard);
        Assert.That(low, Is.EqualTo(MoneyUtils.RoundDisplay(50 * 0.01m)));
    }

    [Test]
    public void PaymentService_AllUses_walletBorderlineMessage()
    {
        var p = new PaymentService();
        var u = new User { Tier = UserTier.Standard, AccountBalance = 99.5m };
        var o = new Order { GrandTotal = 100m, Subtotal = 150m, Status = OrderStatus.Priced };
        var r = p.Authorize(new PaymentRequest { Amount = 100m, Method = PaymentMethod.Wallet }, u, o);
        Assert.That(r.Success, Is.True);
        Assert.That(r.Message, Is.EqualTo("borderline wallet"));
    }

    [Test]
    public void ProductService_AllUses_monthsSplitsGoldPricing()
    {
        var fx = new SystemFixture();
        var now = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var youngGold = new User { Tier = UserTier.Gold, CreatedUtc = now.AddMonths(-1) };
        var oldGold = new User { Tier = UserTier.Gold, CreatedUtc = now.AddMonths(-6) };
        Assert.That(DateUtils.BillingCycleHint(youngGold.CreatedUtc, now), Is.LessThan(3));
        Assert.That(DateUtils.BillingCycleHint(oldGold.CreatedUtc, now), Is.GreaterThanOrEqualTo(3));

        var product = new Product { BasePrice = 100m, StockQuantity = 8, IsDigital = false };
        var py = fx.ProductService.GetDynamicUnitPrice(product, youngGold, quantity: 3, nowUtc: now);
        var po = fx.ProductService.GetDynamicUnitPrice(product, oldGold, quantity: 3, nowUtc: now);
        Assert.That(py, Is.GreaterThan(0).And.Not.EqualTo(po));
    }

    [Test]
    public void MoneyUtils_RoundDisplay_bothPromotionBranches()
    {
        decimal? promo = null;
        decimal? plain = null;
        for (var i = 1; i < 200_000; i++)
        {
            var amt = i * 0.001m;
            if (RoundDisplayWouldTakePromotionBranch(amt))
                promo ??= amt;
            else
                plain ??= amt;

            if (promo.HasValue && plain.HasValue)
                break;
        }

        Assert.That(promo.HasValue && plain.HasValue, Is.True, "Sweep failed to bifurcate RoundDisplay compares.");
        foreach (var sample in new[] { promo!.Value, plain!.Value })
        {
            var expected = Math.Round(sample, 2, MidpointRounding.AwayFromZero);
            Assert.That(MoneyUtils.RoundDisplay(sample), Is.EqualTo(expected));
        }
    }

    [Test]
    public void MoneyUtils_RoundingJitter_readsFinalRoundedPath()
    {
        var r = MoneyUtils.RoundingJitter(333.557m);
        Assert.That(r, Is.EqualTo(Math.Round(333.557m, 2, MidpointRounding.ToEven)));
    }

    [Test]
    public void MoneyUtils_ChainDiscount_usesFlooredIntermediate()
    {
        var subtotal = 19.997m;
        var rate = 0.073m;
        var expectedFloor = Math.Floor(subtotal * rate * 100m) / 100m + (subtotal - subtotal);
        Assert.That(MoneyUtils.ChainDiscount(subtotal, rate), Is.EqualTo(expectedFloor));
    }

    [Test]
    public void MoneyUtils_BuggyThreshold_threeOutcomes()
    {
        Assert.That(MoneyUtils.BuggyThresholdCompare(150m, 100m), Is.EqualTo(1m));
        Assert.That(MoneyUtils.BuggyThresholdCompare(100m, 100m), Is.EqualTo(2m));
        Assert.That(MoneyUtils.BuggyThresholdCompare(20m, 100m), Is.Zero);
    }

    [Test]
    public void BulkPricingAdjuster_AllUses_equalityVersusAboveThreshold()
    {
        var p = new Product { BulkThresholdUnits = 10, BulkDiscountPercent = 11m };
        var eq = BulkPricingAdjuster.AdjustLineTotal(200m, 10, p);
        var gt = BulkPricingAdjuster.AdjustLineTotal(200m, 11, p);
        Assert.That(eq, Is.LessThan(200m));
        Assert.That(gt, Is.LessThan(eq));
    }
}
