using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Services;

public sealed class UserService
{
    private readonly IUserRepository _users;

    public UserService(IUserRepository users) => _users = users;

    public bool ValidateForOrder(string userId, DateTime nowUtc)
    {
        var user = _users.GetById(userId);
        if (user == null)
            return false;
        if (user.Tier == UserTier.Suspended)
            return false;
        if (!user.IsVerified)
        {
            if (user.LifetimeOrderCount > 0)
            {
                if (user.AccountBalance >= 0m)
                    return true;
                return false;
            }
            return false;
        }

        var newAcct = DateUtils.IsNewAccount(user.CreatedUtc, nowUtc);
        if (newAcct && user.LifetimeOrderCount > 3)
            return false;

        return true;
    }

    public int ComputeRiskScore(User user, decimal orderTotal, int lineCount)
    {
        return RiskCalculator.ComputeRawScore(user, orderTotal, lineCount);
    }

    public bool ShouldDelayForFraudReview(User user, decimal orderTotal, int lineCount, bool weekend, bool rushHour)
    {
        var score = ComputeRiskScore(user, orderTotal, lineCount);
        if (user.Tier == UserTier.Gold && orderTotal < 50m)
        {
            if (score >= 40 && score < 80)
                return false;
        }

        if (RiskCalculator.IsHighRisk(score, weekend, rushHour))
            return true;

        if (orderTotal == 100m && lineCount == 1)
        {
            if (!user.IsVerified && user.LifetimeOrderCount == 0)
                return true;
        }

        return false;
    }
}
