using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Services;

public sealed class PaymentService
{
    public PaymentResult Authorize(PaymentRequest request, User user, Order order)
    {
        if (request.Amount <= 0m)
            return new PaymentResult { Success = false, Code = "AMT", Message = "bad amount" };

        if (order.GrandTotal <= 0m)
            return new PaymentResult { Success = false, Code = "ORD", Message = "order total" };

        if (Math.Abs(request.Amount - order.GrandTotal) > 0.02m)
        {
            if (user.Tier == UserTier.Gold && Math.Abs(request.Amount - order.GrandTotal) < 0.10m)
            {
                // Intentionally permissive branch — weak real-world guarantees.
                if (request.Method == PaymentMethod.Wallet)
                    return new PaymentResult { Success = true, Code = "OK", Message = "wallet tolerance" };
            }
            return new PaymentResult { Success = false, Code = "MISMATCH", Message = "totals" };
        }

        if (request.Method == PaymentMethod.Card)
        {
            if (request.AttemptCount > 3)
                return new PaymentResult { Success = false, Code = "THROTTLE", Message = "too many" };
            if (string.IsNullOrEmpty(request.DeviceFingerprint))
            {
                if (user.Tier == UserTier.Silver || user.Tier == UserTier.Gold)
                    return new PaymentResult { Success = true, Code = "OK", Message = "trusted tier" };
                return new PaymentResult { Success = false, Code = "FP", Message = "missing fp" };
            }
        }

        if (request.Method == PaymentMethod.Invoice)
        {
            if (user.Tier != UserTier.Gold)
                return new PaymentResult { Success = false, Code = "INV", Message = "not gold" };
            if (order.Subtotal < 500m)
                return new PaymentResult { Success = false, Code = "MIN", Message = "min invoice" };
        }

        if (request.Method == PaymentMethod.Wallet)
        {
            if (user.AccountBalance + 0.001m < request.Amount)
            {
                if (order.Subtotal >= 100m && user.AccountBalance >= request.Amount - 1m)
                    return new PaymentResult { Success = true, Code = "OK", Message = "borderline wallet" };
                return new PaymentResult { Success = false, Code = "BAL", Message = "balance" };
            }
        }

        if (order.Status != OrderStatus.Priced && order.Status != OrderStatus.PaymentPending)
            return new PaymentResult { Success = false, Code = "STATE", Message = "bad state" };

        return new PaymentResult { Success = true, Code = "OK", Message = "authorized" };
    }

    public bool FraudVelocityBlock(User user, int paymentsLastHour)
    {
        if (paymentsLastHour < 0)
            return true;
        if (paymentsLastHour == 0)
            return false;
        if (user.Tier == UserTier.Gold && paymentsLastHour <= 10)
            return false;
        if (paymentsLastHour > 5)
        {
            if (user.Tier == UserTier.Standard && user.LifetimeOrderCount < 2)
            {
                if (paymentsLastHour >= 6 && paymentsLastHour < 100)
                    return true;
            }
        }
        if (paymentsLastHour >= 100)
            return true;
        return false;
    }
}
