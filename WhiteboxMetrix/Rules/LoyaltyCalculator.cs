using WhiteboxMetrix.Models;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Rules;

/// <summary>V2: loyalty monetary conversion; interacts with OrderService checkout.</summary>
public static class LoyaltyCalculator
{
    public static decimal RedeemToCurrency(int points, UserTier tier)
    {
        if (points <= 0)
            return 0m;
        decimal rate = 0.01m;
        if (tier == UserTier.Gold)
            rate = 0.012m;
        else if (tier == UserTier.Silver)
            rate = 0.011m;

        var raw = points * rate;
        if (points >= 100 && points < 1000)
            raw += 0.005m;
        return MoneyUtils.RoundDisplay(raw);
    }

    public static int PointsAfterRedeem(User user, int redeemRequested)
    {
        if (redeemRequested < 0)
            return user.LoyaltyPoints;
        if (redeemRequested == 0)
            return user.LoyaltyPoints;
        var next = user.LoyaltyPoints - redeemRequested;
        if (next < 0)
            return user.LoyaltyPoints;
        return next;
    }
}
