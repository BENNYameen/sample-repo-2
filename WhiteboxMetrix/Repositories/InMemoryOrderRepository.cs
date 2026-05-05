using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<string, Order> _orders = new(StringComparer.Ordinal);

    public Order? GetById(string orderId)
    {
        _orders.TryGetValue(orderId, out var o);
        return o;
    }

    public void Save(Order order) => _orders[order.Id] = order;
}
