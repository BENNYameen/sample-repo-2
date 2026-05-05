using NUnit.Framework;
using WhiteboxMetrix.Models;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class PricingServiceTests
{
    private SystemFixture _fx = null!;

    [SetUp]
    public void SetUp() => _fx = new SystemFixture();

    [Test]
    public void PriceOrder_sets_non_null_grand_total()
    {
        var u = new User { Id = "u1", Tier = UserTier.Gold, LifetimeOrderCount = 10, IsVerified = true, CreatedUtc = DateTime.UtcNow.AddDays(-400) };
        _fx.Users.Save(u);
        var p = new Product { Id = "pa", BasePrice = 50m, StockQuantity = 20, IsDigital = false, WeightKg = 3m };
        _fx.Products.Save(p);
        var order = new Order
        {
            Id = "o1",
            UserId = "u1",
            Lines = new List<OrderLine> { new() { ProductId = "pa", Quantity = 1 } },
            CreatedUtc = DateTime.UtcNow
        };

        _fx.PricingService.PriceOrder(order, u, DateTime.UtcNow, _fx.Audit, userCouponUseCount: 0, applyBulkV2: false);
        Assert.That(order.GrandTotal, Is.GreaterThanOrEqualTo(0m));
    }
}
