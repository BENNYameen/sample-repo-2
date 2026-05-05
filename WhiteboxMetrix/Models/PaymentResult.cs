namespace WhiteboxMetrix.Models;

public sealed class PaymentResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
