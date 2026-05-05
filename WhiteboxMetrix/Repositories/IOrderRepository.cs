using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

public interface IOrderRepository
{
    Order? GetById(string orderId);
    void Save(Order order);
}
