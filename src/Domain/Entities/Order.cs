using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
/// Order aggregate. Every change goes through a method that enforces the invariants: totals are always
/// recalculated from the lines, the status follows <see cref="OrderStatusIds"/> transitions, and lines
/// can only change while the order is Pending.
/// </summary>
public class Order
{
    public const int MaxLines = 200;
    public const string DefaultCurrency = "USD";

    private readonly List<OrderLine> _lines = new();

    public Guid Id { get; private set; }
    /// <summary><c>{prefix}-{yyyy}-{000000}</c>, unique. Null only for rows written by a pre-v2 container.</summary>
    public string? OrderNumber { get; private set; }
    /// <summary>The buyer: an auth-service user with the <c>customer</c> role. Null only on pre-v2 rows.
    /// The name, tax ID, email and phone below are a snapshot taken when the order was written.</summary>
    public Guid? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public string? CustomerTaxId { get; private set; }
    public string? CustomerEmail { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string Currency { get; private set; } = DefaultCurrency;
    public string? Notes { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal Total { get; private set; }

    // Copy of the payment state, owned by the future payment-service (see ApplyPaymentBalance).
    public decimal AmountPaid { get; private set; }
    public decimal BalanceDue { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Unpaid;
    public long PaymentBalanceVersion { get; private set; }

    public int StatusId { get; private set; }
    public OrderStatus? Status { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // Soft delete: a deleted order is hidden from every query but kept for audit.
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>). Clients send it back on every write.</summary>
    public uint Version { get; private set; }

    private Order() {}

    public static Order Create(string orderNumber, Guid customerId, CustomerSnapshot customer, string? currency, string? notes,
        IEnumerable<OrderLine> lines, Guid? userId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) throw new ArgumentException("Order number is required.", nameof(orderNumber));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            StatusId = OrderStatusIds.Pending,
            CreatedAt = now,
            CreatedBy = userId,
        };
        order.ApplyDetails(customerId, customer, currency, notes, lines);
        return order;
    }

    public bool IsEditable => StatusId == OrderStatusIds.Pending && !IsDeleted;

    public void UpdateDetails(Guid customerId, CustomerSnapshot customer, string? currency, string? notes,
        IEnumerable<OrderLine> lines, Guid? userId, DateTime now)
    {
        EnsureNotDeleted();
        if (StatusId != OrderStatusIds.Pending)
            throw new DomainConflictException($"Only pending orders can be edited. This order is {OrderStatusNames.Of(StatusId)}.");
        ApplyDetails(customerId, customer, currency, notes, lines);
        Touch(userId, now);
    }

    public void Confirm(Guid? userId, DateTime now)
    {
        EnsureTransition(OrderStatusIds.Confirmed, OrderStatusIds.Pending);
        if (_lines.Count == 0) throw new DomainConflictException("An order needs at least one line before it can be confirmed.");
        StatusId = OrderStatusIds.Confirmed;
        ConfirmedAt = now;
        Touch(userId, now);
    }

    public void Ship(Guid? userId, DateTime now)
    {
        EnsureTransition(OrderStatusIds.Shipped, OrderStatusIds.Confirmed);
        StatusId = OrderStatusIds.Shipped;
        ShippedAt = now;
        Touch(userId, now);
    }

    public void Deliver(Guid? userId, DateTime now)
    {
        EnsureTransition(OrderStatusIds.Delivered, OrderStatusIds.Shipped);
        StatusId = OrderStatusIds.Delivered;
        DeliveredAt = now;
        Touch(userId, now);
    }

    public void Cancel(string? reason, Guid? userId, DateTime now)
    {
        EnsureTransition(OrderStatusIds.Cancelled, OrderStatusIds.Pending, OrderStatusIds.Confirmed);
        if (AmountPaid > 0)
            throw new DomainConflictException("This order has payments. Refund them before cancelling it.");
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length < 3) throw new DomainValidationException("A cancellation reason of at least 3 characters is required.");
        if (trimmed.Length > 500) throw new DomainValidationException("The cancellation reason is longer than 500 characters.");
        StatusId = OrderStatusIds.Cancelled;
        CancelReason = trimmed;
        CancelledAt = now;
        Touch(userId, now);
    }

    /// <summary>
    /// Logical removal. Only orders that never moved forward (Pending) or were cancelled, and that have no
    /// payments, may be removed; anything else is a financial record and must stay visible.
    /// </summary>
    public void SoftDelete(Guid? userId, DateTime now)
    {
        EnsureNotDeleted();
        if (StatusId != OrderStatusIds.Pending && StatusId != OrderStatusIds.Cancelled)
            throw new DomainConflictException(
                $"Only pending or cancelled orders can be deleted. This order is {OrderStatusNames.Of(StatusId)}; cancel it first.");
        if (AmountPaid > 0)
            throw new DomainConflictException("Orders with payments cannot be deleted.");
        IsDeleted = true;
        DeletedAt = now;
        DeletedBy = userId;
        Touch(userId, now);
    }

    /// <summary>
    /// Applies a balance computed by the payment-service. Out-of-order messages are ignored: a version that is
    /// not newer than the stored one returns false and changes nothing.
    /// </summary>
    public bool ApplyPaymentBalance(decimal amountPaid, decimal balanceDue, PaymentStatus status, long version)
    {
        if (version <= PaymentBalanceVersion) return false;
        if (amountPaid < 0 || balanceDue < 0)
            throw new DomainValidationException("Payment amounts cannot be negative.");
        AmountPaid = Money.Round(amountPaid);
        BalanceDue = Money.Round(balanceDue);
        PaymentStatus = status;
        PaymentBalanceVersion = version;
        return true;
    }

    private void ApplyDetails(Guid customerId, CustomerSnapshot customer, string? currency, string? notes, IEnumerable<OrderLine> lines)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (customerId == Guid.Empty) throw new DomainValidationException("Select the customer (an auth-service customer).");
        var newLines = lines?.ToList() ?? new List<OrderLine>();
        if (newLines.Count == 0) throw new DomainValidationException("An order needs at least one line.");
        if (newLines.Count > MaxLines) throw new DomainValidationException($"An order can have at most {MaxLines} lines.");

        var code = string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
            throw new DomainValidationException("Currency must be a 3-letter ISO code such as USD.");

        var trimmedNotes = notes?.Trim();
        if (trimmedNotes?.Length > 1000) throw new DomainValidationException("Notes are longer than 1000 characters.");

        CustomerId = customerId;
        CustomerName = customer.Name;
        CustomerTaxId = customer.TaxId;
        CustomerEmail = customer.Email;
        CustomerPhone = customer.Phone;
        Currency = code;
        Notes = string.IsNullOrEmpty(trimmedNotes) ? null : trimmedNotes;

        _lines.Clear();
        _lines.AddRange(newLines);
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        Subtotal = _lines.Sum(l => l.LineGross);
        DiscountTotal = _lines.Sum(l => l.DiscountAmount);
        TaxTotal = _lines.Sum(l => l.LineTax);
        Total = Subtotal - DiscountTotal + TaxTotal;
        // Lines only change while Pending, before any payment-service balance exists.
        BalanceDue = Math.Max(0, Total - AmountPaid);
    }

    private void EnsureTransition(int target, params int[] allowedFrom)
    {
        EnsureNotDeleted();
        if (!allowedFrom.Contains(StatusId))
            throw new DomainConflictException(
                $"Cannot move an order from {OrderStatusNames.Of(StatusId)} to {OrderStatusNames.Of(target)}.");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted) throw new DomainConflictException("This order has been deleted.");
    }

    private void Touch(Guid? userId, DateTime now)
    {
        UpdatedAt = now;
        UpdatedBy = userId;
    }
}

public static class OrderStatusNames
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [OrderStatusIds.Pending] = "Pending",
        [OrderStatusIds.Paid] = "Paid",
        [OrderStatusIds.Shipped] = "Shipped",
        [OrderStatusIds.Delivered] = "Delivered",
        [OrderStatusIds.Cancelled] = "Cancelled",
        [OrderStatusIds.Confirmed] = "Confirmed",
    };

    public static string Of(int statusId) => Names.TryGetValue(statusId, out var name) ? name : $"status {statusId}";
}
