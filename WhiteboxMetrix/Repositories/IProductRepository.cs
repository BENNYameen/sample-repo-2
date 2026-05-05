using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

public interface IProductRepository
{
    Product? GetById(string productId);
    void Save(Product product);
}
