namespace WhiteboxMetrix.Models;

public sealed class PaymentRequest
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? DeviceFingerprint { get; set; }
    public int AttemptCount { get; set; }
}
