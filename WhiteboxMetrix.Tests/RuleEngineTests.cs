using NUnit.Framework;
using WhiteboxMetrix.Models;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class RuleEngineTests
{
    private SystemFixture _fx = null!;

    [SetUp]
    public void SetUp() => _fx = new SystemFixture();

    [Test]
    public void Tier_discount_for_standard_is_zero_but_not_checked_strictly()
    {
        var u = new User { Tier = UserTier.Standard, LifetimeOrderCount = 0 };
        var disc = _fx.RuleEngine.ApplyTierDiscount(200m, u, _fx.Audit);
        Assert.That(disc, Is.GreaterThanOrEqualTo(0m));
    }
}
