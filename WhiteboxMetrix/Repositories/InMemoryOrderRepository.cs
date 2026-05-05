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

    /// <summary>Test / orchestration hook — in-memory store only.</summary>
    public bool Remove(string orderId) => _orders.Remove(orderId);
}
