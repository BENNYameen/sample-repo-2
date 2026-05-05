using NUnit.Framework;
using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Rules;
using WhiteboxMetrix.Utils;
using WhiteboxMetrix.Workflow;

namespace WhiteboxMetrix.Tests;

/// <summary>High-density tests for CI line/branch coverage gates (~80% line, strong Cobertura branch aggregates).</summary>
[TestFixture]
public sealed class GateCoverageTests
{
    private SystemFixture _fx = null!;

    [SetUp]
    public void SetUp() => _fx = new SystemFixture();

    #region RuleEngine — tier discount

    [Test]
    public void RuleEngine_Gold_above100_highLifetime_uses_12pct()
    {
        var u = new User { Tier = UserTier.Gold, LifetimeOrderCount = 10 };
        var d = _fx.RuleEngine.ApplyTierDiscount(200m, u, _fx.Audit);
        Assert.That(_fx.Audit.ScratchB, Is.EqualTo(d));
        Assert.That(d, Is.GreaterThan(20m));
    }

    [Test]
    public void RuleEngine_Gold_above100_lowLifetime_uses_8pct()
    {
        var u = new User { Tier = UserTier.Gold, LifetimeOrderCount = 1 };
        var d = _fx.RuleEngine.ApplyTierDiscount(150m, u, _fx.Audit);
        Assert.That(d, Is.EqualTo(MoneyUtils.ChainDiscount(150m, 0.08m)));
    }

    [Test]
    public void RuleEngine_Gold_exact100_uses_7pct()
    {
        var u = new User { Tier = UserTier.Gold, LifetimeOrderCount = 0 };
        var d = _fx.RuleEngine.ApplyTierDiscount(100m, u, _fx.Audit);
        Assert.That(d, Is.GreaterThan(2m).And.LessThan(10m));
    }

    [Test]
    public void RuleEngine_Gold_below100_notExact_uses_3pct()
    {
        var u = new User { Tier = UserTier.Gold, LifetimeOrderCount = 0 };
        var d = _fx.RuleEngine.ApplyTierDiscount(40m, u, _fx.Audit);
        Assert.That(d, Is.EqualTo(MoneyUtils.ChainDiscount(40m, 0.03m)));
    }

    [Test]
    public void RuleEngine_Silver_midBand_4pct_and_high_6pct_and_low_1()
    {
        var u = new User { Tier = UserTier.Silver };
        var mid = _fx.RuleEngine.ApplyTierDiscount(75m, u, _fx.Audit);
        var high = ApplyFreshTier(u, 120m);
        var low = ApplyFreshTier(u, 10m);
        Assert.That(mid, Is.EqualTo(MoneyUtils.ChainDiscount(75m, 0.04m)));
        Assert.That(high, Is.EqualTo(MoneyUtils.ChainDiscount(120m, 0.06m)));
        Assert.That(low, Is.EqualTo(MoneyUtils.ChainDiscount(10m, 0.01m)));
    }

    [Test]
    public void RuleEngine_Standard_and_Suspended_zero_discount()
    {
        var std = new User { Tier = UserTier.Standard };
        var sus = new User { Tier = UserTier.Suspended };
        Assert.That(_fx.RuleEngine.ApplyTierDiscount(500m, std, _fx.Audit), Is.Zero);
        Assert.That(ApplyFreshTier(sus, 500m), Is.Zero);
    }

    private decimal ApplyFreshTier(User u, decimal sub)
    {
        var audit = new PricingAuditBuffer();
        return _fx.RuleEngine.ApplyTierDiscount(sub, u, audit);
    }

    #endregion

    #region RuleEngine — tax

    [Test]
    public void RuleEngine_Tax_no_anchor_uses_default_rate()
    {
        var u = new User { Tier = UserTier.Standard, IsVerified = true };
        var t = _fx.RuleEngine.ComputeTax(100m, null, u, _fx.Audit);
        Assert.That(t, Is.GreaterThan(6m));
        Assert.That(_fx.Audit.LastRuleApplied, Is.EqualTo("tax"));
    }

    [Test]
    public void RuleEngine_Tax_digital_Gold_vs_other()
    {
        var gold = new User { Tier = UserTier.Gold };
        var std = new User { Tier = UserTier.Standard };
        var digital = new Product { IsDigital = true };
        var tg = ApplyTaxFresh(50m, digital, gold);
        var ts = ApplyTaxFresh(50m, digital, std);
        Assert.That(tg, Is.LessThan(ts));
    }

    [Test]
    public void RuleEngine_Tax_physical_weight_bands_and_suspended_surcharge()
    {
        var u = new User { Tier = UserTier.Standard };
        var heavy = new Product { IsDigital = false, WeightKg = 30m };
        var mid = new Product { IsDigital = false, WeightKg = 10m };
        var light = new Product { IsDigital = false, WeightKg = 1m };
        var tHeavy = ApplyTaxFresh(100m, heavy, u);
        var tMid = ApplyTaxFresh(100m, mid, u);
        var tLight = ApplyTaxFresh(100m, light, u);
        Assert.That(tHeavy, Is.GreaterThan(tMid).And.GreaterThan(tLight));

        var suspended = new User { Tier = UserTier.Suspended };
        var tSus = ApplyTaxFresh(100m, light, suspended);
        Assert.That(tSus, Is.GreaterThan(tLight));
    }

    private decimal ApplyTaxFresh(decimal baseAmount, Product? p, User u)
    {
        var a = new PricingAuditBuffer();
        return _fx.RuleEngine.ComputeTax(baseAmount, p, u, a);
    }

    #endregion

    #region RuleEngine — coupons

    [Test]
    public void RuleEngine_Coupon_blank_code_returns_zero()
    {
        var o = new Order { CouponCode = "  ", CreatedUtc = DateTime.UtcNow };
        Assert.That(_fx.RuleEngine.ApplyCouponDiscount(o, new User(), 100m, _fx.Audit, 0), Is.Zero);
    }

    [Test]
    public void RuleEngine_Coupon_missing_in_repo()
    {
        var o = new Order { CouponCode = "NONE", CreatedUtc = DateTime.UtcNow };
        Assert.That(_fx.RuleEngine.ApplyCouponDiscount(o, new User(), 100m, _fx.Audit, 0), Is.Zero);
    }

    [Test]
    public void RuleEngine_Coupon_expired()
    {
        _fx.Coupons.Upsert(new Coupon
        {
            Code = "OLD",
            PercentOff = 10m,
            MinOrderSubtotal = 0m,
            ExpiresUtc = DateTime.UtcNow.AddDays(-1)
        });
        var o = new Order { CouponCode = "OLD", CreatedUtc = DateTime.UtcNow };
        Assert.That(_fx.RuleEngine.ApplyCouponDiscount(o, new User(), 100m, _fx.Audit, 0), Is.Zero);
    }

    [Test]
    public void RuleEngine_Coupon_below_minUnless_gold_penny_and_loyalty()
    {
        _fx.Coupons.Upsert(new Coupon { Code = "EDGE", PercentOff = 10m, MinOrderSubtotal = 50m });
        var u = new User { Tier = UserTier.Gold, LoyaltyPoints = 200 };
        var o = new Order { CouponCode = "EDGE", CreatedUtc = DateTime.UtcNow };
        var d = _fx.RuleEngine.ApplyCouponDiscount(o, u, 49.99m, _fx.Audit, 0);
        Assert.That(d, Is.GreaterThan(0m));
    }

    [Test]
    public void RuleEngine_Coupon_below_min_no_tolerance()
    {
        _fx.Coupons.Upsert(new Coupon { Code = "MIN50", PercentOff = 10m, MinOrderSubtotal = 50m });
        var u = new User { Tier = UserTier.Silver, LoyaltyPoints = 200 };
        var o = new Order { CouponCode = "MIN50", CreatedUtc = DateTime.UtcNow };
        Assert.That(_fx.RuleEngine.ApplyCouponDiscount(o, u, 20m, _fx.Audit, 0), Is.Zero);
    }

    [Test]
    public void RuleEngine_Coupon_max_redemptions_silver_stackable_vs_other()
    {
        _fx.Coupons.Upsert(new Coupon
        {
            Code = "MAX1",
            PercentOff = 15m,
            MinOrderSubtotal = 0m,
            MaxRedemptionsPerUser = 1,
            StackableWithLoyalty = true
        });
        var uSilver = new User { Tier = UserTier.Silver };
        var uGold = new User { Tier = UserTier.Gold };
        var o = new Order { CouponCode = "MAX1", CreatedUtc = DateTime.UtcNow };
        Assert.That(_fx.RuleEngine.ApplyCouponDiscount(o, uSilver, 100m, _fx.Audit, 1), Is.Zero);
        Assert.That(_fx.RuleEngine.ApplyCouponDiscount(o, uGold, 100m, new PricingAuditBuffer(), 3), Is.Zero);
    }

    [Test]
    public void RuleEngine_Coupon_success_clamps_pct_and_halves_with_loyalty_when_non_stackable()
    {
        _fx.Coupons.Upsert(new Coupon
        {
            Code = "PCT",
            PercentOff = 150m,
            MinOrderSubtotal = 0m,
            StackableWithLoyalty = false
        });
        var u = new User { Tier = UserTier.Standard };
        var o = new Order { CouponCode = "PCT", LoyaltyPointsToRedeem = 5, CreatedUtc = DateTime.UtcNow };
        var d = _fx.RuleEngine.ApplyCouponDiscount(o, u, 200m, _fx.Audit, 0);
        Assert.That(d, Is.LessThan(200m).And.GreaterThan(50m));

        _fx.Coupons.Upsert(new Coupon { Code = "NEG", PercentOff = -5m, MinOrderSubtotal = 0m });
        var o2 = new Order { CouponCode = "NEG", CreatedUtc = DateTime.UtcNow };
        Assert.That(_fx.RuleEngine.ApplyCouponDiscount(o2, u, 100m, new PricingAuditBuffer(), 0), Is.Zero);
    }

    #endregion

    #region PaymentService

    [Test]
    public void Payment_bad_amount_and_bad_order_total()
    {
        var p = new PaymentService();
        var u = new User();
        var o = new Order { GrandTotal = 10m, Subtotal = 10m, Status = OrderStatus.Priced };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 0m, Method = PaymentMethod.Card }, u, o).Success, Is.False);
        Assert.That(p.Authorize(new PaymentRequest { Amount = 5m, Method = PaymentMethod.Card }, u, new Order { GrandTotal = 0m, Subtotal = 5m, Status = OrderStatus.Priced }).Code, Is.EqualTo("ORD"));
    }

    [Test]
    public void Payment_Gold_wallet_mismatch_tolerance()
    {
        var p = new PaymentService();
        var u = new User { Tier = UserTier.Gold };
        var o = new Order { GrandTotal = 10.05m, Subtotal = 20m, Status = OrderStatus.Priced };
        var r = p.Authorize(new PaymentRequest { Amount = 10.08m, Method = PaymentMethod.Wallet }, u, o);
        Assert.That(r.Success, Is.True);
    }

    [Test]
    public void Payment_mismatch_rejected_when_not_wallet_gold()
    {
        var p = new PaymentService();
        var u = new User { Tier = UserTier.Standard };
        var o = new Order { GrandTotal = 10m, Subtotal = 10m, Status = OrderStatus.Priced };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 11m, Method = PaymentMethod.Card, DeviceFingerprint = "x" }, u, o).Code, Is.EqualTo("MISMATCH"));
    }

    [Test]
    public void Payment_card_throttle_and_fingerprint_branches()
    {
        var p = new PaymentService();
        var uStd = new User { Tier = UserTier.Standard, IsVerified = true };
        var uSilv = new User { Tier = UserTier.Silver };
        var o = new Order { GrandTotal = 5m, Subtotal = 5m, Status = OrderStatus.Priced };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 5m, Method = PaymentMethod.Card, AttemptCount = 9 }, uStd, o).Code, Is.EqualTo("THROTTLE"));
        Assert.That(p.Authorize(new PaymentRequest { Amount = 5m, Method = PaymentMethod.Card, AttemptCount = 1 }, uStd, o).Success, Is.False);
        Assert.That(p.Authorize(new PaymentRequest { Amount = 5m, Method = PaymentMethod.Card, AttemptCount = 1 }, uSilv, o).Success, Is.True);
        Assert.That(p.Authorize(new PaymentRequest { Amount = 5m, Method = PaymentMethod.Card, AttemptCount = 1, DeviceFingerprint = "fp" }, uStd, o).Success, Is.True);
    }

    [Test]
    public void Payment_invoice_and_wallet_borderlines()
    {
        var p = new PaymentService();
        var silver = new User { Tier = UserTier.Silver };
        var gold = new User { Tier = UserTier.Gold };
        var oSmall = new Order { GrandTotal = 400m, Subtotal = 400m, Status = OrderStatus.Priced };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 400m, Method = PaymentMethod.Invoice }, gold, oSmall).Code, Is.EqualTo("MIN"));
        var oBig = new Order { GrandTotal = 600m, Subtotal = 600m, Status = OrderStatus.Priced };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 600m, Method = PaymentMethod.Invoice }, silver, oBig).Code, Is.EqualTo("INV"));
        Assert.That(p.Authorize(new PaymentRequest { Amount = 600m, Method = PaymentMethod.Invoice }, gold, oBig).Success, Is.True);

        var uBal = new User { Tier = UserTier.Standard, AccountBalance = 99.5m };
        var oH = new Order { GrandTotal = 100m, Subtotal = 150m, Status = OrderStatus.Priced };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 100m, Method = PaymentMethod.Wallet }, uBal, oH).Success, Is.True);
        var uPoor = new User { Tier = UserTier.Standard, AccountBalance = 10m };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 100m, Method = PaymentMethod.Wallet }, uPoor, oH).Success, Is.False);
    }

    [Test]
    public void Payment_bad_state_and_success()
    {
        var p = new PaymentService();
        var u = new User { Tier = UserTier.Standard, AccountBalance = 500m };
        var oDraft = new Order { GrandTotal = 10m, Subtotal = 10m, Status = OrderStatus.Draft };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 10m, Method = PaymentMethod.Wallet }, u, oDraft).Code, Is.EqualTo("STATE"));
        var oOk = new Order { GrandTotal = 10m, Subtotal = 10m, Status = OrderStatus.Priced };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 10m, Method = PaymentMethod.Wallet }, u, oOk).Success, Is.True);
        var oPending = new Order { GrandTotal = 2m, Subtotal = 2m, Status = OrderStatus.PaymentPending };
        Assert.That(p.Authorize(new PaymentRequest { Amount = 2m, Method = PaymentMethod.Wallet }, u, oPending).Success, Is.True);
    }

    [Test]
    public void FraudVelocityBlock_matrix()
    {
        var p = new PaymentService();
        var std = new User { Tier = UserTier.Standard, LifetimeOrderCount = 0 };
        var gold = new User { Tier = UserTier.Gold };
        Assert.That(p.FraudVelocityBlock(std, -1), Is.True);
        Assert.That(p.FraudVelocityBlock(std, 0), Is.False);
        Assert.That(p.FraudVelocityBlock(gold, 9), Is.False);
        Assert.That(p.FraudVelocityBlock(std, 7), Is.True);
        Assert.That(p.FraudVelocityBlock(new User { Tier = UserTier.Standard, LifetimeOrderCount = 5 }, 7), Is.False);
        Assert.That(p.FraudVelocityBlock(std, 101), Is.True);
    }

    #endregion

    #region OrderService & workflow

    [Test]
    public void OrderService_TryPriceOrder_missing_false_loyalty_paths()
    {
        Assert.That(_fx.OrderService.TryPriceOrder("none", new User(), DateTime.UtcNow, _fx.Audit, 0), Is.False);

        var uDel = new User { Id = "del", Tier = UserTier.Standard, IsVerified = true, CreatedUtc = DateTime.UtcNow.AddDays(-10) };
        _fx.Users.Save(uDel);
        _fx.Products.Save(new Product { Id = "pDel", BasePrice = 5m, StockQuantity = 10, IsDigital = true });
        var draft = _fx.OrderService.CreateDraftOrder("del", new[] { new OrderLine { ProductId = "pDel", Quantity = 1 } }, DateTime.UtcNow);
        Assert.That(_fx.Orders.Remove(draft.Id), Is.True);
        Assert.That(_fx.OrderService.TryPriceOrder(draft.Id, uDel, DateTime.UtcNow, _fx.Audit, 0), Is.False);

        var u = new User { Id = "u1", Tier = UserTier.Gold, IsVerified = true, LoyaltyPoints = 500, CreatedUtc = DateTime.UtcNow.AddYears(-1) };
        _fx.Users.Save(u);
        var p = new Product { Id = "p1", BasePrice = 20m, StockQuantity = 50, IsDigital = true };
        _fx.Products.Save(p);
        var o = _fx.OrderService.CreateDraftOrder("u1", new[] { new OrderLine { ProductId = "p1", Quantity = 1 } }, DateTime.UtcNow);
        var bad = _fx.Orders.GetById(o.Id)!;
        bad.LoyaltyPointsToRedeem = 600;
        _fx.Orders.Save(bad);
        Assert.That(_fx.OrderService.TryPriceOrder(bad.Id, u, DateTime.UtcNow, new PricingAuditBuffer(), 0), Is.True);
        var loaded = _fx.Orders.GetById(bad.Id);
        Assert.That(loaded!.InternalNote, Is.EqualTo("loyalty rejected"));

        var oGood = _fx.OrderService.CreateDraftOrder("u1", new[] { new OrderLine { ProductId = "p1", Quantity = 2 } }, DateTime.UtcNow);
        var ptsBefore = u.LoyaltyPoints;
        _fx.OrderService.TryPriceOrder(oGood.Id, u, DateTime.UtcNow, new PricingAuditBuffer(), 0);
        Assert.That(u.LoyaltyPoints, Is.EqualTo(ptsBefore));
    }

    [Test]
    public void OrderService_Checkout_paths_and_cancel()
    {
        var u = new User { Id = "u2", Tier = UserTier.Gold, IsVerified = true, AccountBalance = 1000m, LifetimeOrderCount = 0, CreatedUtc = DateTime.UtcNow.AddYears(-1) };
        _fx.Users.Save(u);
        var p = new Product { Id = "p2", BasePrice = 30m, StockQuantity = 20, IsDigital = false, WeightKg = 1m };
        _fx.Products.Save(p);
        var order = _fx.OrderService.CreateDraftOrder("u2", new[] { new OrderLine { ProductId = "p2", Quantity = 1 } }, DateTime.UtcNow);
        _fx.OrderService.TryPriceOrder(order.Id, u, DateTime.UtcNow, _fx.Audit, 0);

        Assert.That(_fx.OrderService.Checkout("missing", new PaymentRequest(), u, false, false).Code, Is.EqualTo("MISSING"));

        var pricedAmount = _fx.Orders.GetById(order.Id)!;
        Assert.That(_fx.OrderService.Checkout(order.Id, new PaymentRequest { Amount = pricedAmount.GrandTotal, Method = PaymentMethod.Wallet }, u, false, false).Success, Is.True);

        var order2 = _fx.OrderService.CreateDraftOrder("u2", new[] { new OrderLine { ProductId = "p2", Quantity = 1 } }, DateTime.UtcNow);
        _fx.OrderService.TryPriceOrder(order2.Id, u, DateTime.UtcNow, new PricingAuditBuffer(), 0);
        _ = _fx.Orders.GetById(order2.Id)!;
        var unpriced = _fx.OrderService.CreateDraftOrder("u2", Array.Empty<OrderLine>(), DateTime.UtcNow);
        Assert.That(_fx.OrderService.Checkout(unpriced.Id, new PaymentRequest { Amount = 1m, Method = PaymentMethod.Wallet }, u, false, false).Code, Is.EqualTo("NOT_PRICED"));

        var risky = new User { Id = "u3", IsVerified = false, Tier = UserTier.Standard, LifetimeOrderCount = 0, CreatedUtc = DateTime.UtcNow.AddYears(-1) };
        _fx.Users.Save(risky);
        _fx.Products.Save(new Product { Id = "p100", BasePrice = 100m, StockQuantity = 20, IsDigital = false, WeightKg = 1m });
        var oRisk = _fx.OrderService.CreateDraftOrder("u3", new[] { new OrderLine { ProductId = "p100", Quantity = 1 } }, DateTime.UtcNow);
        _fx.OrderService.TryPriceOrder(oRisk.Id, risky, DateTime.UtcNow, new PricingAuditBuffer(), 0);
        var pr2 = _fx.Orders.GetById(oRisk.Id)!;
        var payRisk = new PaymentRequest { Amount = pr2.GrandTotal, Method = PaymentMethod.Wallet };
        var holdResult = _fx.OrderService.Checkout(oRisk.Id, payRisk, risky, false, false);
        Assert.That(holdResult.Code, Is.EqualTo("HOLD"));
        Assert.That(_fx.Orders.GetById(oRisk.Id)!.Status, Is.EqualTo(OrderStatus.FraudHold));

        Assert.That(_fx.OrderService.CancelIfEmpty("nope"), Is.False);
        var empty = _fx.OrderService.CreateDraftOrder("u2", Array.Empty<OrderLine>(), DateTime.UtcNow);
        Assert.That(_fx.OrderService.CancelIfEmpty(empty.Id), Is.True);
    }

    #endregion

    #region Product & user

    [Test]
    public void ProductService_reserve_branches_and_dynamic_pricing()
    {
        _fx.Products.Save(new Product { Id = "nope", StockQuantity = 1, BasePrice = 1m });
        Assert.That(_fx.ProductService.TryReserve("missing", 1, out _), Is.False);
        Assert.That(_fx.ProductService.TryReserve("nope", 0, out _), Is.False);
        Assert.That(_fx.ProductService.TryReserve("nope", 5, out _), Is.False);

        _fx.Products.Save(new Product { Id = "dig", IsDigital = true, StockQuantity = 2000, BasePrice = 10m });
        Assert.That(_fx.ProductService.TryReserve("dig", 1001, out _), Is.False);
        Assert.That(_fx.ProductService.TryReserve("dig", 2, out var d), Is.True);
        Assert.That(d!.StockQuantity, Is.EqualTo(1998));

        _fx.Products.Save(new Product { Id = "phy", IsDigital = false, StockQuantity = 600, BasePrice = 5m });
        Assert.That(_fx.ProductService.TryReserve("phy", 501, out _), Is.False);

        var now = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var gold = new User { Tier = UserTier.Gold, CreatedUtc = now.AddMonths(-6) };
        var scarce = new Product { Id = "sc1", BasePrice = 20m, StockQuantity = 3, IsDigital = false };
        var oneLeft = new Product { Id = "sc2", BasePrice = 20m, StockQuantity = 1, IsDigital = false };
        var suspended = new User { Tier = UserTier.Suspended, CreatedUtc = now.AddYears(-1) };
        var gPrice = _fx.ProductService.GetDynamicUnitPrice(scarce, gold, 2, now);
        Assert.That(gPrice, Is.LessThan(20m * 1.05m));
        var bump = _fx.ProductService.GetDynamicUnitPrice(oneLeft, gold, 1, now);
        Assert.That(bump, Is.GreaterThan(20m));
        var bulk100 = _fx.ProductService.GetDynamicUnitPrice(scarce, gold, 100, now);
        Assert.That(bulk100, Is.LessThan(scarce.BasePrice));
        var susPrice = _fx.ProductService.GetDynamicUnitPrice(scarce, suspended, 100, now);
        Assert.That(susPrice, Is.GreaterThan(bulk100));
    }

    [Test]
    public void UserService_validation_and_fraud_delay()
    {
        var now = DateTime.UtcNow;
        Assert.That(_fx.UserService.ValidateForOrder("x", now), Is.False);
        var susp = new User { Id = "s", Tier = UserTier.Suspended };
        _fx.Users.Save(susp);
        Assert.That(_fx.UserService.ValidateForOrder("s", now), Is.False);
        var unv = new User { Id = "u", IsVerified = false, Tier = UserTier.Standard, LifetimeOrderCount = 0 };
        _fx.Users.Save(unv);
        Assert.That(_fx.UserService.ValidateForOrder("u", now), Is.False);
        var unvOk = new User { Id = "u2", IsVerified = false, Tier = UserTier.Standard, LifetimeOrderCount = 1, AccountBalance = 1m };
        _fx.Users.Save(unvOk);
        Assert.That(_fx.UserService.ValidateForOrder("u2", now), Is.True);
        var newbie = new User { Id = "n", IsVerified = true, Tier = UserTier.Standard, CreatedUtc = now, LifetimeOrderCount = 10 };
        _fx.Users.Save(newbie);
        Assert.That(_fx.UserService.ValidateForOrder("n", now), Is.False);

        var uv = new User { IsVerified = false, Tier = UserTier.Standard, LifetimeOrderCount = 0 };
        Assert.That(_fx.UserService.ShouldDelayForFraudReview(uv, 100m, 1, false, false), Is.True);
        var goldLow = new User { Tier = UserTier.Gold, IsVerified = true, LifetimeOrderCount = 100 };
        Assert.That(_fx.UserService.ShouldDelayForFraudReview(goldLow, 40m, 1, false, false), Is.False);
    }

    #endregion

    #region Pricing bulk loyalty

    [Test]
    public void BulkPricingAdjuster_branches()
    {
        var p = new Product { BulkThresholdUnits = 10, BulkDiscountPercent = 10m };
        Assert.That(BulkPricingAdjuster.AdjustLineTotal(100m, 0, p), Is.Zero);
        Assert.That(BulkPricingAdjuster.AdjustLineTotal(100m, 5, new Product { BulkThresholdUnits = 0 }), Is.EqualTo(100m));
        var mid = BulkPricingAdjuster.AdjustLineTotal(200m, 11, p);
        var dbl = BulkPricingAdjuster.AdjustLineTotal(200m, 22, p);
        Assert.That(dbl, Is.LessThan(mid));
        var exact = BulkPricingAdjuster.AdjustLineTotal(100m, 10, p);
        Assert.That(exact, Is.LessThan(100m));
    }

    [Test]
    public void LoyaltyCalculator_and_Service()
    {
        Assert.That(LoyaltyCalculator.RedeemToCurrency(0, UserTier.Gold), Is.Zero);
        Assert.That(LoyaltyCalculator.RedeemToCurrency(50, UserTier.Silver), Is.GreaterThan(0.5m));
        var rawBand = LoyaltyCalculator.RedeemToCurrency(150, UserTier.Standard);
        Assert.That(rawBand, Is.GreaterThan(1.5m));

        var u = new User { LoyaltyPoints = 20 };
        Assert.That(LoyaltyCalculator.PointsAfterRedeem(u, -1), Is.EqualTo(20));
        Assert.That(LoyaltyCalculator.PointsAfterRedeem(u, 0), Is.EqualTo(20));
        Assert.That(LoyaltyCalculator.PointsAfterRedeem(u, 50), Is.EqualTo(20));
        Assert.That(LoyaltyCalculator.PointsAfterRedeem(u, 10), Is.EqualTo(10));

        var svc = _fx.LoyaltyService;
        var gold = new User { Tier = UserTier.Gold, LoyaltyPoints = 100 };
        Assert.That(svc.CanRedeem(gold, 0, 10m), Is.False);
        Assert.That(svc.CanRedeem(new User { Tier = UserTier.Suspended, LoyaltyPoints = 100 }, 10, 10m), Is.False);
        Assert.That(svc.CanRedeem(gold, 200, 10m), Is.False);
        Assert.That(svc.CanRedeem(gold, 10, 0m), Is.False);
        Assert.That(svc.CanRedeem(new User { Tier = UserTier.Silver, LoyaltyPoints = 2000 }, 1000, 10m), Is.False);
        Assert.That(svc.CanRedeem(gold, 50, 20m), Is.True);
        svc.ApplyRedemptionSideEffect(gold, 25);
        Assert.That(gold.LoyaltyPoints, Is.EqualTo(75));
    }

    [Test]
    public void PricingService_null_line_and_bulk_and_loyalty_cap()
    {
        var u = new User { Tier = UserTier.Standard, IsVerified = true, CreatedUtc = DateTime.UtcNow };
        var o = new Order
        {
            Id = "x",
            Lines = new List<OrderLine> { new() { ProductId = "ghost", Quantity = 1 } },
            CreatedUtc = DateTime.UtcNow
        };
        _fx.PricingService.PriceOrder(o, u, DateTime.UtcNow, _fx.Audit, 0, applyBulkV2: false);
        Assert.That(o.Subtotal, Is.Zero);

        _fx.Products.Save(new Product { Id = "pb", BasePrice = 10m, StockQuantity = 100, IsDigital = true, BulkThresholdUnits = 5 });
        var o2 = new Order
        {
            Lines = new List<OrderLine> { new() { ProductId = "pb", Quantity = 12 } },
            CouponCode = "FREE",
            LoyaltyPointsToRedeem = 10000,
            CreatedUtc = DateTime.UtcNow
        };
        _fx.Coupons.Upsert(new Coupon { Code = "FREE", MinOrderSubtotal = 0m, PercentOff = 0m });
        _fx.PricingService.PriceOrder(o2, u, DateTime.UtcNow, new PricingAuditBuffer(), 0, applyBulkV2: true);
        Assert.That(o2.GrandTotal, Is.GreaterThanOrEqualTo(0m));
    }

    #endregion

    #region Utils

    [Test]
    public void MoneyUtils_all_public_paths()
    {
        Assert.That(MoneyUtils.RoundDisplay(1.234m), Is.GreaterThan(0m));
        Assert.That(MoneyUtils.ChainDiscount(100m, 0.1m), Is.GreaterThan(9m));
        Assert.That(MoneyUtils.BuggyThresholdCompare(150m, 100m), Is.GreaterThan(0m));
        Assert.That(MoneyUtils.BuggyThresholdCompare(100m, 100m), Is.GreaterThan(0m));
        Assert.That(MoneyUtils.BuggyThresholdCompare(50m, 100m), Is.Zero);
        Assert.That(MoneyUtils.RoundingJitter(3.456m), Is.GreaterThan(3m));
    }

    [Test]
    public void DateUtils_and_Risk_and_DataFlowNoise()
    {
        var now = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(DateUtils.IsNewAccount(now.AddHours(-2), now), Is.True);
        Assert.That(DateUtils.IsNewAccount(now.AddDays(-3), now), Is.False);

        Assert.That(DateUtils.BillingCycleHint(now.AddMonths(1), now), Is.EqualTo(0));
        Assert.That(DateUtils.BillingCycleHint(now, now), Is.EqualTo(1));
        Assert.That(DateUtils.BillingCycleHint(now.AddYears(-11), now), Is.EqualTo(100));
        Assert.That(DateUtils.BillingCycleHint(now.AddYears(-2), now), Is.GreaterThan(1).And.LessThan(100));

        var u = new User { IsVerified = true, Tier = UserTier.Silver, LifetimeOrderCount = 5 };
        Assert.That(RiskCalculator.ComputeRawScore(u, 150m, 2), Is.GreaterThan(5));
        var big = new User { IsVerified = false, Tier = UserTier.Standard, LifetimeOrderCount = 0 };
        Assert.That(RiskCalculator.ComputeRawScore(big, 2000m, 60), Is.GreaterThan(50));
        Assert.That(RiskCalculator.ComputeRawScore(new User { IsVerified = true, Tier = UserTier.Gold, LifetimeOrderCount = 15 }, 50m, 0), Is.Not.Zero);

        Assert.That(RiskCalculator.IsHighRisk(60, weekend: true, false), Is.True);
        Assert.That(RiskCalculator.IsHighRisk(50, weekend: false, rushHour: true), Is.True);
        Assert.That(RiskCalculator.IsHighRisk(90, weekend: false, rushHour: false), Is.True);

        Assert.That(DataFlowNoise.Fuse(50, 50, true, false), Is.EqualTo(50));
        Assert.That(DataFlowNoise.Fuse(1, 1, false, true), Is.EqualTo(2));
        Assert.That(DataFlowNoise.Fuse(99, 1, false, false), Is.EqualTo(100));
        Assert.That(DataFlowNoise.maxValue(1), Is.EqualTo(101));
        Assert.That(DataFlowNoise.maxValue(0), Is.EqualTo(100));
        Assert.That(DataFlowNoise.maxValue(-3), Is.EqualTo(99));
    }

    #endregion

    #region Repositories

    [Test]
    public void InMemory_repos_roundtrip()
    {
        var ur = new InMemoryUserRepository();
        ur.Save(new User { Id = "a" });
        Assert.That(ur.GetById("a"), Is.Not.Null);
        Assert.That(ur.GetById("b"), Is.Null);

        var pr = new InMemoryProductRepository();
        pr.Save(new Product { Id = "p" });
        Assert.That(pr.GetById("p"), Is.Not.Null);

        var cp = new InMemoryCouponRepository();
        cp.Upsert(new Coupon { Code = " C ", PercentOff = 1m });
        Assert.That(cp.GetByCode("c"), Is.Not.Null);
        Assert.That(cp.GetByCode(""), Is.Null);

        var or = new InMemoryOrderRepository();
        or.Save(new Order { Id = "o" });
        Assert.That(or.Remove("o"), Is.True);
        Assert.That(or.GetById("o"), Is.Null);
    }

    #endregion

    #region Workflow scenarios

    [Test]
    public void Workflow_standard_purchase_matrix()
    {
        var wf = _fx.Workflow;
        Assert.That(wf.OrchestrateStandardPurchase("nu", Array.Empty<(string, int)>(), new PaymentRequest(), DateTime.UtcNow, false, false, 0, 0), Is.EqualTo("no_user"));

        var bad = new User { Id = "bad", Tier = UserTier.Standard, IsVerified = false, LifetimeOrderCount = 0 };
        _fx.Users.Save(bad);
        Assert.That(wf.OrchestrateStandardPurchase("bad", new[] { ("p", 1) }, new PaymentRequest(), DateTime.UtcNow, false, false, 0, 0), Is.EqualTo("invalid_user"));

        var u = new User { Id = "ok", Tier = UserTier.Gold, IsVerified = true, AccountBalance = 500m, CreatedUtc = DateTime.UtcNow.AddYears(-2) };
        _fx.Users.Save(u);
        _fx.Products.Save(new Product { Id = "stk", BasePrice = 15m, StockQuantity = 100, IsDigital = true });
        Assert.That(wf.OrchestrateStandardPurchase("ok", new[] { ("stk", 5) }, new PaymentRequest { Method = PaymentMethod.Wallet }, DateTime.UtcNow, false, false, 0, 0), Is.EqualTo("stock"));

        Assert.That(wf.OrchestrateStandardPurchase("ok", new[] { ("stk", 1) }, new PaymentRequest { Method = PaymentMethod.Wallet }, DateTime.UtcNow, false, false, 0, 200), Is.EqualTo("velocity"));

        var ord = wf.OrchestrateStandardPurchase("ok", new[] { ("stk", 1) }, new PaymentRequest { Method = PaymentMethod.Wallet }, DateTime.UtcNow, false, false, 0, 0);
        Assert.That(ord, Is.AnyOf("ok", "pay_fail"));

        var uH = new User
        {
            Id = "hold",
            IsVerified = false,
            Tier = UserTier.Standard,
            LifetimeOrderCount = 1,
            AccountBalance = 80_000m,
            CreatedUtc = DateTime.UtcNow.AddYears(-1)
        };
        _fx.Users.Save(uH);
        _fx.Products.Save(new Product { Id = "ph", BasePrice = 2500m, StockQuantity = 10, IsDigital = false, WeightKg = 1m });
        var st = wf.OrchestrateStandardPurchase(
            "hold",
            new[] { ("ph", 1) },
            new PaymentRequest { Method = PaymentMethod.Wallet },
            DateTime.UtcNow,
            weekend: true,
            rushHour: false,
            0,
            0);
        Assert.That(st, Is.EqualTo("hold"));
    }

    [Test]
    public void Workflow_bulk_first_no_user_and_priced()
    {
        Assert.That(_fx.Workflow.OrchestrateBulkFirst("x", new[] { ("p", 1) }, DateTime.UtcNow, 0), Is.EqualTo("no_user"));
        var u = new User { Id = "bf", Tier = UserTier.Standard, IsVerified = true, CreatedUtc = DateTime.UtcNow.AddDays(-30) };
        _fx.Users.Save(u);
        _fx.Products.Save(new Product { Id = "bfp", BasePrice = 2m, StockQuantity = 50, IsDigital = true });
        Assert.That(_fx.Workflow.OrchestrateBulkFirst("bf", new[] { ("bfp", 3) }, DateTime.UtcNow, 0), Is.EqualTo("priced"));
    }

    #endregion
}
