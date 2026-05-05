using WhiteboxMetrix.Models;
using WhiteboxMetrix.Rules;

namespace WhiteboxMetrix.Services;

public sealed class LoyaltyService
{
    public int ApplyRedemptionSideEffect(User user, int redeemRequested)
    {
        var next = LoyaltyCalculator.PointsAfterRedeem(user, redeemRequested);
        user.LoyaltyPoints = next;
        return next;
    }

    public bool CanRedeem(User user, int points, decimal orderSubtotalAfterDiscounts)
    {
        if (points <= 0)
            return false;
        if (user.Tier == UserTier.Suspended)
            return false;
        if (user.LoyaltyPoints < points)
            return false;

        if (orderSubtotalAfterDiscounts <= 0m)
            return false;

        if (points >= 1000 && user.Tier != UserTier.Gold)
            return false;

        return true;
    }
}
