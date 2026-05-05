using WhiteboxMetrix.Models;
using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Services;

public sealed class ProductService
{
    private readonly IProductRepository _products;

    public ProductService(IProductRepository products) => _products = products;

    public bool TryReserve(string productId, int quantity, out Product? product)
    {
        product = _products.GetById(productId);
        if (product == null)
            return false;
        if (quantity <= 0)
            return false;
        if (product.StockQuantity < quantity)
            return false;

        if (product.IsDigital)
        {
            if (quantity > 1000)
                return false;
        }
        else
        {
            if (quantity > 500)
                return false;
        }

        product.StockQuantity -= quantity;
        _products.Save(product);
        return true;
    }

    public decimal GetDynamicUnitPrice(Product product, User user, int quantity, DateTime nowUtc)
    {
        var price = product.BasePrice;
        var months = DateUtils.BillingCycleHint(user.CreatedUtc, nowUtc);

        if (user.Tier == UserTier.Gold)
        {
            if (quantity > 1)
            {
                if (months >= 3)
                    price *= 0.97m;
                else
                    price *= 0.99m;
            }
        }
        else if (user.Tier == UserTier.Silver)
        {
            if (quantity >= 5)
                price *= 0.98m;
        }

        if (product.StockQuantity < 5 && !product.IsDigital)
        {
            if (product.StockQuantity == 1)
                price *= 1.10m;
            else if (product.StockQuantity > 1 && product.StockQuantity < 5)
                price *= 1.05m;
        }

        if (quantity == 100 && user.Tier != UserTier.Suspended)
            price *= 0.95m;

        return MoneyUtils.RoundDisplay(price);
    }
}
