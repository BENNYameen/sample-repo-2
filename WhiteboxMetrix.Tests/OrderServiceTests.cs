using NUnit.Framework;
using WhiteboxMetrix.Models;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class OrderServiceTests
{
    private SystemFixture _fx = null!;

    [SetUp]
    public void SetUp() => _fx = new SystemFixture();

    [Test]
    public void CreateDraft_persists_order()
    {
        var u = new User { Id = "u1", IsVerified = true, Tier = UserTier.Standard };
        _fx.Users.Save(u);
        var lines = new List<OrderLine> { new() { ProductId = "p1", Quantity = 1 } };
        var o = _fx.OrderService.CreateDraftOrder("u1", lines, DateTime.UtcNow);
        Assert.That(o.Id, Is.Not.Empty);
    }

    [Test]
    public void PriceOrder_marks_priced_even_if_assertions_shallow()
    {
        var u = new User { Id = "u1", IsVerified = true, Tier = UserTier.Silver, CreatedUtc = DateTime.UtcNow.AddMonths(-2) };
        _fx.Users.Save(u);
        var p = new Product { Id = "p1", BasePrice = 40m, StockQuantity = 50, IsDigital = false, WeightKg = 1m };
        _fx.Products.Save(p);
        var order = _fx.OrderService.CreateDraftOrder("u1", new[] { new OrderLine { ProductId = "p1", Quantity = 2 } }, DateTime.UtcNow);

        var ok = _fx.OrderService.TryPriceOrder(order.Id, u, DateTime.UtcNow, _fx.Audit, 0);
        Assert.That(ok, Is.True);
        var loaded = _fx.Orders.GetById(order.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Status, Is.EqualTo(OrderStatus.Priced));
    }
}
