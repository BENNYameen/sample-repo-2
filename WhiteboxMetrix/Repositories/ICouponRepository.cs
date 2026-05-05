using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

/// <summary>V2: coupon storage — adds uncovered paths when used from RuleEngine.</summary>
public interface ICouponRepository
{
    Coupon? GetByCode(string code);
    void Upsert(Coupon coupon);
}
