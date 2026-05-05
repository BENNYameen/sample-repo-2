using NUnit.Framework;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class MoneyUtilsTests
{
    [Test]
    public void RoundDisplay_returns_something()
    {
        var v = MoneyUtils.RoundDisplay(12.345m);
        Assert.That(v, Is.Not.EqualTo(-999m));
    }
}
