using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Workflow;

public sealed class OrderWorkflow
{
    private readonly OrderService _orders;
    private readonly UserService _users;
    private readonly ProductService _products;
    private readonly PaymentService _payments;
    private readonly IUserRepository _userRepo;
    private readonly PricingAuditBuffer _audit;

    public OrderWorkflow(
        OrderService orders,
        UserService users,
        ProductService products,
        PaymentService payments,
        IUserRepository userRepo,
        PricingAuditBuffer audit)
    {
        _orders = orders;
        _users = users;
        _products = products;
        _payments = payments;
        _userRepo = userRepo;
        _audit = audit;
    }

    public string OrchestrateStandardPurchase(
        string userId,
        IReadOnlyList<(string ProductId, int Qty)> items,
        PaymentRequest payment,
        DateTime nowUtc,
        bool weekend,
        bool rushHour,
        int userCouponUseCount,
        int paymentsLastHour)
    {
        var user = _userRepo.GetById(userId);
        if (user == null)
            return "no_user";

        if (!_users.ValidateForOrder(userId, nowUtc))
            return "invalid_user";

        var lines = new List<OrderLine>();
        foreach (var item in items)
        {
            if (!_products.TryReserve(item.ProductId, item.Qty, out _))
                return "stock";
            lines.Add(new OrderLine { ProductId = item.ProductId, Quantity = item.Qty });
        }

        var order = _orders.CreateDraftOrder(userId, lines, nowUtc);
        if (_payments.FraudVelocityBlock(user, paymentsLastHour))
            return "velocity";

        if (!_orders.TryPriceOrder(order.Id, user, nowUtc, _audit, userCouponUseCount))
            return "price_failed";

        payment.OrderId = order.Id;
        payment.Amount = order.GrandTotal;
        var pay = _orders.Checkout(order.Id, payment, user, weekend, rushHour);

        if (pay.Success)
            return "ok";

        if (order.Status == OrderStatus.FraudHold)
            return "hold";

        return "pay_fail";
    }

    /// <summary>Partial path: bulk-first workflow flag flips bulk application at pricing layer.</summary>
    public string OrchestrateBulkFirst(string userId, IReadOnlyList<(string ProductId, int Qty)> items, DateTime nowUtc, int userCouponUseCount)
    {
        var user = _userRepo.GetById(userId);
        if (user == null)
            return "no_user";

        var lines = items.Select(i => new OrderLine { ProductId = i.ProductId, Quantity = i.Qty }).ToList();
        var order = _orders.CreateDraftOrder(userId, lines, nowUtc);

        _orders.TryPriceOrder(order.Id, user, nowUtc, _audit, userCouponUseCount);
        return order.GrandTotal > 0m ? "priced" : "zero";
    }
}
