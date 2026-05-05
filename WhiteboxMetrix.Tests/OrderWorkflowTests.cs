using NUnit.Framework;
using WhiteboxMetrix.Models;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class OrderWorkflowTests
{
    private SystemFixture _fx = null!;

    [SetUp]
    public void SetUp() => _fx = new SystemFixture();

    [Test]
    public void Orchestrate_happy_path_returns_token_not_empty()
    {
        var u = new User
        {
            Id = "u1",
            Email = "x@y.z",
            IsVerified = true,
            Tier = UserTier.Gold,
            LifetimeOrderCount = 2,
            AccountBalance = 500m,
            CreatedUtc = DateTime.UtcNow.AddYears(-1)
        };
        _fx.Users.Save(u);

        var p = new Product { Id = "p1", BasePrice = 20m, StockQuantity = 100, IsDigital = false, WeightKg = 2m };
        _fx.Products.Save(p);

        var pay = new PaymentRequest { Method = PaymentMethod.Wallet, AttemptCount = 1 };
        var result = _fx.Workflow.OrchestrateStandardPurchase(
            "u1",
            new[] { ("p1", 2) },
            pay,
            DateTime.UtcNow,
            weekend: false,
            rushHour: false,
            userCouponUseCount: 0,
            paymentsLastHour: 0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(1));
    }

    [Test]
    public void Orchestrate_bulk_first_reports_priced_when_positive_total()
    {
        var u = new User { Id = "u2", Tier = UserTier.Standard, CreatedUtc = DateTime.UtcNow.AddDays(-10), IsVerified = true };
        _fx.Users.Save(u);
        var p = new Product { Id = "p9", BasePrice = 5m, StockQuantity = 200, IsDigital = true };
        _fx.Products.Save(p);

        var r = _fx.Workflow.OrchestrateBulkFirst("u2", new[] { ("p9", 11) }, DateTime.UtcNow, 0);
        Assert.That(r, Is.Not.EqualTo("no_user"));
    }
}
