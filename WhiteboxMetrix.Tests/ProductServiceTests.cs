using NUnit.Framework;
using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class ProductServiceTests
{
    private SystemFixture _fx = null!;

    [SetUp]
    public void SetUp() => _fx = new SystemFixture();

    [Test]
    public void Reserve_reduces_stock()
    {
        var p = new Product { Id = "p1", BasePrice = 10m, StockQuantity = 5, IsDigital = false };
        _fx.Products.Save(p);
        var ok = _fx.ProductService.TryReserve("p1", 2, out var after);
        Assert.That(ok, Is.True);
        Assert.That(after, Is.Not.Null);
    }

    [Test]
    public void Dynamic_price_returns_positive()
    {
        var p = new Product { Id = "p2", BasePrice = 25m, StockQuantity = 20, IsDigital = false };
        var u = new User { Id = "u1", Tier = UserTier.Silver, CreatedUtc = DateTime.UtcNow.AddMonths(-6) };
        var price = _fx.ProductService.GetDynamicUnitPrice(p, u, 3, DateTime.UtcNow);
        Assert.That(price, Is.GreaterThan(0m));
    }
}
