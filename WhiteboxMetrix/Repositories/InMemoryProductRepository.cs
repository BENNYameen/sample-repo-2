using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<string, Product> _products = new(StringComparer.Ordinal);

    public Product? GetById(string productId)
    {
        _products.TryGetValue(productId, out var p);
        return p;
    }

    public void Save(Product product) => _products[product.Id] = product;
}
