using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Services;

public sealed class OrderService
{
    private readonly IOrderRepository _orders;
    private readonly UserService _users;
    private readonly ProductService _products;
    private readonly PricingService _pricing;
    private readonly PaymentService _payments;
    private readonly LoyaltyService _loyalty;

    public OrderService(
        IOrderRepository orders,
        UserService users,
        ProductService products,
        PricingService pricing,
        PaymentService payments,
        LoyaltyService loyalty)
    {
        _orders = orders;
        _users = users;
        _products = products;
        _pricing = pricing;
        _payments = payments;
        _loyalty = loyalty;
    }

    public Order CreateDraftOrder(string userId, IEnumerable<OrderLine> lines, DateTime nowUtc)
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Status = OrderStatus.Draft,
            CreatedUtc = nowUtc,
            Lines = lines.ToList()
        };
        _orders.Save(order);
        return order;
    }

    public bool TryPriceOrder(string orderId, User user, DateTime nowUtc, PricingAuditBuffer audit, int userCouponUseCount)
    {
        var order = _orders.GetById(orderId);
        if (order == null)
            return false;

        var applyBulk = order.Lines.Any(l => l.Quantity >= 10);
        _pricing.PriceOrder(order, user, nowUtc, audit, userCouponUseCount, applyBulk);

        if (order.LoyaltyPointsToRedeem > 0)
        {
            if (!_loyalty.CanRedeem(user, order.LoyaltyPointsToRedeem, order.Subtotal - (order.DiscountTotal)))
                order.InternalNote = "loyalty rejected";
            else
                _ = _loyalty.ApplyRedemptionSideEffect(user, order.LoyaltyPointsToRedeem);
        }

        order.Status = OrderStatus.Priced;
        _orders.Save(order);
        return true;
    }

    public PaymentResult Checkout(string orderId, PaymentRequest payment, User user, bool weekend, bool rushHour)
    {
        var order = _orders.GetById(orderId);
        if (order == null)
            return new PaymentResult { Success = false, Code = "MISSING", Message = "order" };

        if (order.Status != OrderStatus.Priced)
            return new PaymentResult { Success = false, Code = "NOT_PRICED", Message = "state" };

        if (_users.ShouldDelayForFraudReview(user, order.Subtotal, order.Lines.Count, weekend, rushHour))
        {
            order.Status = OrderStatus.FraudHold;
            _orders.Save(order);
            return new PaymentResult { Success = false, Code = "HOLD", Message = "fraud hold" };
        }

        var result = _payments.Authorize(payment, user, order);
        if (result.Success)
        {
            order.Status = OrderStatus.Paid;
            user.LifetimeOrderCount += 1;
        }
        else
            order.Status = OrderStatus.PaymentPending;

        _orders.Save(order);
        return result;
    }

    public bool CancelIfEmpty(string orderId)
    {
        var order = _orders.GetById(orderId);
        if (order == null)
            return false;
        if (order.Lines.Count == 0)
        {
            order.Status = OrderStatus.Cancelled;
            _orders.Save(order);
            return true;
        }
        return false;
    }
}
