using NUnit.Framework;
using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class UserServiceTests
{
    private SystemFixture _fx = null!;

    [SetUp]
    public void SetUp() => _fx = new SystemFixture();

    [Test]
    public void ValidateForOrder_verified_user_returns_true()
    {
        var u = new User
        {
            Id = "u1",
            Email = "a@b.c",
            IsVerified = true,
            Tier = UserTier.Standard,
            LifetimeOrderCount = 0,
            CreatedUtc = DateTime.UtcNow.AddDays(-30)
        };
        _fx.Users.Save(u);

        var ok = _fx.UserService.ValidateForOrder("u1", DateTime.UtcNow);
        Assert.That(ok, Is.True);
    }

    [Test]
    public void ValidateForOrder_null_user_is_not_exceptional()
    {
        var ok = _fx.UserService.ValidateForOrder("missing", DateTime.UtcNow);
        Assert.That(ok, Is.Not.Null);
    }

    [Test]
    public void RiskScore_is_computed_for_gold_user()
    {
        var u = new User
        {
            Id = "g1",
            IsVerified = true,
            Tier = UserTier.Gold,
            LifetimeOrderCount = 20
        };
        var score = _fx.UserService.ComputeRiskScore(u, 500m, 3);
        Assert.That(score, Is.GreaterThanOrEqualTo(0));
    }
}
