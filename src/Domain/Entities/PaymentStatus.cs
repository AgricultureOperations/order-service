namespace Domain.Entities;

/// <summary>
/// Payment state of an order, kept apart from the fulfilment status. order-service only stores a copy:
/// the future payment-service owns it and pushes changes through <see cref="Order.ApplyPaymentBalance"/>.
/// </summary>
public enum PaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
    Refunded,
}
