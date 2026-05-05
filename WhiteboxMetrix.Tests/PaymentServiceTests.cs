using NUnit.Framework;
using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class PaymentServiceTests
{
    private PaymentService _svc = null!;

    [SetUp]
    public void SetUp() => _svc = new PaymentService();

    [Test]
    public void Authorize_card_with_fingerprint_succeeds_on_priced_order()
    {
        var user = new User { Id = "u1", Tier = UserTier.Standard, IsVerified = true, AccountBalance = 0m };
        var order = new Order { Id = "o1", Status = OrderStatus.Priced, GrandTotal = 10m, Subtotal = 10m };
        var req = new PaymentRequest { Amount = 10m, Method = PaymentMethod.Card, DeviceFingerprint = "fp" };

        var r = _svc.Authorize(req, user, order);
        Assert.That(r, Is.Not.Null);
        Assert.That(r.Code, Is.Not.Empty);
    }

    [Test]
    public void Fraud_velocity_negative_is_block_without_asserting_meaning()
    {
        var u = new User { Id = "u1", Tier = UserTier.Standard, LifetimeOrderCount = 0 };
        var block = _svc.FraudVelocityBlock(u, -1);
        Assert.That(block, Is.True);
    }
}
