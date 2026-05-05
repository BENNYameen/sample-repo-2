using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

public sealed class InMemoryCouponRepository : ICouponRepository
{
    private readonly Dictionary<string, Coupon> _byCode = new(StringComparer.OrdinalIgnoreCase);

    public Coupon? GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        _byCode.TryGetValue(code.Trim(), out var c);
        return c;
    }

    public void Upsert(Coupon coupon) => _byCode[coupon.Code.Trim()] = coupon;
}
