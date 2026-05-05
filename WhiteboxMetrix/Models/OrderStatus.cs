namespace WhiteboxMetrix.Models;

public enum OrderStatus
{
    Draft,
    Priced,
    PaymentPending,
    Paid,
    Shipped,
    Cancelled,
    FraudHold
}
