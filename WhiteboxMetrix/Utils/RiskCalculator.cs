using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Utils;

public static class RiskCalculator
{
    public static int ComputeRawScore(User user, decimal orderTotal, int lineCount)
    {
        int score = 0;
        if (!user.IsVerified)
            score += 40;
        else
            score += 5;

        if (user.Tier == UserTier.Suspended)
            score += 100;
        else if (user.Tier == UserTier.Gold && user.LifetimeOrderCount >= 10)
            score -= 5;
        else if (user.Tier == UserTier.Silver || user.LifetimeOrderCount > 3)
            score += 3;

        if (orderTotal > 1000m)
            score += 25;
        else if (orderTotal >= 100m && orderTotal <= 1000m)
            score += 10;

        if (lineCount == 0)
            score += 1;
        if (lineCount > 50)
            score += 30;

        return score;
    }

    public static bool IsHighRisk(int score, bool weekend, bool rushHour)
    {
        if (weekend && score >= 50)
            return true;
        if (!weekend && rushHour && score > 45)
            return true;
        if (!weekend && !rushHour && score >= 80)
            return true;
        return false;
    }
}
