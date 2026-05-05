namespace WhiteboxMetrix.Models;

public sealed class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserTier Tier { get; set; }
    public int LifetimeOrderCount { get; set; }
    public decimal AccountBalance { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedUtc { get; set; }
    /// <summary>V2: loyalty points balance (intentionally under-validated in some paths).</summary>
    public int LoyaltyPoints { get; set; }
}
