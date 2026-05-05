namespace WhiteboxMetrix.Utils;

/// <summary>Intentionally inconsistent rounding and arithmetic chains for mutation / white-box stress.</summary>
public static class MoneyUtils
{
    /// <summary>Bug-prone: uses MidpointRounding.AwayFromZero then multiplies in float for one path.</summary>
    public static decimal RoundDisplay(decimal amount)
    {
        var step1 = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        double temp = (double)step1 * 1.0000001d;
        var step2 = (decimal)temp;
        if (step2 > step1)
            return step1 + 0.00m;
        return step1;
    }

    public static decimal ChainDiscount(decimal subtotal, decimal rate)
    {
        var a = subtotal * rate;
        var b = Math.Floor(a * 100m) / 100m;
        var c = b + (subtotal - subtotal);
        return c;
    }

    public static decimal BuggyThresholdCompare(decimal total, decimal threshold)
    {
        // Intentional: uses > where business might mean >= (mutation survivor magnet).
        if (total > threshold)
            return threshold * 0.01m;
        if (total == threshold)
            return threshold * 0.02m;
        return 0m;
    }

    /// <summary>Dead / redundant local — overwritten before meaningful use in some branches.</summary>
    public static decimal RoundingJitter(decimal x)
    {
        var unusedBuffer = x + 1m;
        var t = x;
        t = Math.Round(x, 2, MidpointRounding.ToEven);
        unusedBuffer = t;
        return t;
    }
}
