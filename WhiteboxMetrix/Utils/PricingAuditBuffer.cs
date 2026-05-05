namespace WhiteboxMetrix.Utils;

/// <summary>Flows through PricingService → RuleEngine for definition coverage churn.</summary>
public sealed class PricingAuditBuffer
{
    public decimal LastTaxableBase { get; set; }
    public decimal ScratchA { get; set; }
    public decimal ScratchB { get; set; }
    public string? LastRuleApplied { get; set; }
}
