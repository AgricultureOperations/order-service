using Application.Tests.Support;
using Domain.Entities;
using Domain.Exceptions;

namespace Application.Tests.Domain;

public class OrderTotalsTests
{
    [Fact]
    public void Totals_are_calculated_from_the_lines()
    {
        var order = TestData.PendingOrder(
            TestData.Line(quantity: 3, unitPrice: 25.50m, discount: 6.50m, taxRate: 0.15m),
            TestData.Line(quantity: 1.5m, unitPrice: 10m, discount: 0, taxRate: 0m, sku: "SEED-002"));

        // Line 1: gross 76.50, subtotal 70.00, tax 10.50. Line 2: gross 15.00, tax 0.
        Assert.Equal(91.50m, order.Subtotal);
        Assert.Equal(6.50m, order.DiscountTotal);
        Assert.Equal(10.50m, order.TaxTotal);
        Assert.Equal(95.50m, order.Total);
        Assert.Equal(95.50m, order.BalanceDue);
        Assert.Equal(0m, order.AmountPaid);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
    }

    [Fact]
    public void Line_amounts_round_half_away_from_zero_to_cents()
    {
        // 0.333 × 1.50 = 0.4995 → 0.50; tax 0.50 × 0.15 = 0.075 → 0.08.
        var line = TestData.Line(quantity: 0.333m, unitPrice: 1.50m, taxRate: 0.15m);
        Assert.Equal(0.50m, line.LineGross);
        Assert.Equal(0.08m, line.LineTax);
        Assert.Equal(0.58m, line.LineTotal);
    }

    [Theory]
    [InlineData(0, 1, 0, 0.15)]       // quantity must be > 0
    [InlineData(1.2345, 1, 0, 0.15)]  // quantity has 4 decimals
    [InlineData(1, -1, 0, 0.15)]      // negative price
    [InlineData(1, 1.001, 0, 0.15)]   // price has 3 decimals
    [InlineData(1, 10, 11, 0.15)]     // discount above the line amount
    [InlineData(1, 10, -1, 0.15)]     // negative discount
    [InlineData(1, 10, 0, 1.5)]       // tax rate above 1
    public void Invalid_lines_are_rejected(decimal quantity, decimal price, decimal discount, decimal taxRate)
    {
        Assert.Throws<DomainValidationException>(() => TestData.Line(quantity, price, discount, taxRate));
    }

    [Fact]
    public void An_order_needs_at_least_one_line()
    {
        Assert.Throws<DomainValidationException>(() =>
            Order.Create("ORD-2026-000001", TestData.CustomerId, TestData.Customer(), "USD", null, Array.Empty<OrderLine>(), TestData.UserId, TestData.Now));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("US1")]
    [InlineData("DOLLAR")]
    public void Currency_must_be_a_three_letter_code(string currency)
    {
        Assert.Throws<DomainValidationException>(() =>
            Order.Create("ORD-2026-000001", TestData.CustomerId, TestData.Customer(), currency, null, new[] { TestData.Line() }, TestData.UserId, TestData.Now));
    }

    [Fact]
    public void Currency_defaults_to_USD_and_is_upper_cased()
    {
        var a = Order.Create("ORD-2026-000001", TestData.CustomerId, TestData.Customer(), null, null, new[] { TestData.Line() }, null, TestData.Now);
        var b = Order.Create("ORD-2026-000002", TestData.CustomerId, TestData.Customer(), "eur", null, new[] { TestData.Line() }, null, TestData.Now);
        Assert.Equal("USD", a.Currency);
        Assert.Equal("EUR", b.Currency);
    }

    [Fact]
    public void An_order_needs_an_auth_service_customer()
    {
        Assert.Throws<DomainValidationException>(() =>
            Order.Create("ORD-2026-000001", Guid.Empty, TestData.Customer(), "USD", null, new[] { TestData.Line() }, TestData.UserId, TestData.Now));
    }

    [Fact]
    public void A_new_order_is_pending_and_records_who_created_it()
    {
        var order = TestData.PendingOrder();
        Assert.Equal(OrderStatusIds.Pending, order.StatusId);
        Assert.Equal("ORD-2026-000001", order.OrderNumber);
        Assert.Equal(TestData.UserId, order.CreatedBy);
        Assert.Equal(TestData.CustomerId, order.CustomerId);
        Assert.Equal(TestData.Now, order.CreatedAt);
        Assert.True(order.IsEditable);
    }
}

public class OrderStatusTransitionTests
{
    public static TheoryData<int, string> Allowed => new()
    {
        { OrderStatusIds.Pending, "confirm" },
        { OrderStatusIds.Pending, "cancel" },
        { OrderStatusIds.Confirmed, "ship" },
        { OrderStatusIds.Confirmed, "cancel" },
        { OrderStatusIds.Shipped, "deliver" },
    };

    public static TheoryData<int, string> Forbidden => new()
    {
        { OrderStatusIds.Pending, "ship" },
        { OrderStatusIds.Pending, "deliver" },
        { OrderStatusIds.Confirmed, "confirm" },
        { OrderStatusIds.Confirmed, "deliver" },
        { OrderStatusIds.Shipped, "confirm" },
        { OrderStatusIds.Shipped, "ship" },
        { OrderStatusIds.Shipped, "cancel" },
        { OrderStatusIds.Delivered, "confirm" },
        { OrderStatusIds.Delivered, "ship" },
        { OrderStatusIds.Delivered, "deliver" },
        { OrderStatusIds.Delivered, "cancel" },
        { OrderStatusIds.Cancelled, "confirm" },
        { OrderStatusIds.Cancelled, "ship" },
        { OrderStatusIds.Cancelled, "deliver" },
        { OrderStatusIds.Cancelled, "cancel" },
    };

    private static readonly Dictionary<string, int> Targets = new()
    {
        ["confirm"] = OrderStatusIds.Confirmed,
        ["ship"] = OrderStatusIds.Shipped,
        ["deliver"] = OrderStatusIds.Delivered,
        ["cancel"] = OrderStatusIds.Cancelled,
    };

    private static void Apply(Order order, string action)
    {
        switch (action)
        {
            case "confirm": order.Confirm(TestData.UserId, TestData.Now); break;
            case "ship": order.Ship(TestData.UserId, TestData.Now); break;
            case "deliver": order.Deliver(TestData.UserId, TestData.Now); break;
            case "cancel": order.Cancel("Out of stock", TestData.UserId, TestData.Now); break;
        }
    }

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Allowed_transitions_change_the_status_and_stamp_the_time(int from, string action)
    {
        var order = TestData.OrderIn(from);
        Apply(order, action);
        Assert.Equal(Targets[action], order.StatusId);
        Assert.Equal(TestData.Now, order.UpdatedAt);
        Assert.Equal(TestData.UserId, order.UpdatedBy);
    }

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void Forbidden_transitions_throw_a_conflict(int from, string action)
    {
        var order = TestData.OrderIn(from);
        Assert.Throws<DomainConflictException>(() => Apply(order, action));
        Assert.Equal(from, order.StatusId);
    }

    [Fact]
    public void Transition_timestamps_are_recorded()
    {
        var order = TestData.OrderIn(OrderStatusIds.Delivered);
        Assert.NotNull(order.ConfirmedAt);
        Assert.NotNull(order.ShippedAt);
        Assert.NotNull(order.DeliveredAt);
        Assert.Null(order.CancelledAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  no ")]
    public void Cancel_requires_a_reason(string? reason)
    {
        var order = TestData.PendingOrder();
        Assert.Throws<DomainValidationException>(() => order.Cancel(reason, TestData.UserId, TestData.Now));
        Assert.Equal(OrderStatusIds.Pending, order.StatusId);
    }

    [Fact]
    public void Cancel_stores_the_trimmed_reason()
    {
        var order = TestData.PendingOrder();
        order.Cancel("  Duplicate order  ", TestData.UserId, TestData.Now);
        Assert.Equal("Duplicate order", order.CancelReason);
        Assert.Equal(TestData.Now, order.CancelledAt);
    }

    [Fact]
    public void An_order_with_payments_cannot_be_cancelled()
    {
        var order = TestData.OrderIn(OrderStatusIds.Confirmed);
        order.ApplyPaymentBalance(5m, order.Total - 5m, PaymentStatus.PartiallyPaid, 1);
        Assert.Throws<DomainConflictException>(() => order.Cancel("Customer request", TestData.UserId, TestData.Now));
    }

    [Fact]
    public void Lines_can_only_be_edited_while_pending()
    {
        var order = TestData.OrderIn(OrderStatusIds.Confirmed);
        Assert.False(order.IsEditable);
        Assert.Throws<DomainConflictException>(() =>
            order.UpdateDetails(TestData.CustomerId, TestData.Customer(), "USD", null, new[] { TestData.Line(quantity: 5) }, TestData.UserId, TestData.Now));
    }

    [Fact]
    public void Editing_a_pending_order_replaces_the_lines_and_recalculates()
    {
        var order = TestData.PendingOrder(TestData.Line(quantity: 1, unitPrice: 10m, taxRate: 0m));
        order.UpdateDetails(TestData.CustomerId, TestData.Customer("Finca Nueva"), "USD", "Deliver to gate 2",
            new[] { TestData.Line(quantity: 4, unitPrice: 10m, taxRate: 0m), TestData.Line(quantity: 1, unitPrice: 5m, taxRate: 0m, sku: "X-2") },
            TestData.UserId, TestData.Now);

        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(45m, order.Total);
        Assert.Equal("Finca Nueva", order.CustomerName);
        Assert.Equal("Deliver to gate 2", order.Notes);
    }
}

public class OrderSoftDeleteTests
{
    [Theory]
    [InlineData(OrderStatusIds.Pending)]
    [InlineData(OrderStatusIds.Cancelled)]
    public void Pending_or_cancelled_orders_can_be_soft_deleted(int statusId)
    {
        var order = TestData.OrderIn(statusId);
        order.SoftDelete(TestData.UserId, TestData.Now);
        Assert.True(order.IsDeleted);
        Assert.Equal(TestData.Now, order.DeletedAt);
        Assert.Equal(TestData.UserId, order.DeletedBy);
    }

    [Theory]
    [InlineData(OrderStatusIds.Confirmed)]
    [InlineData(OrderStatusIds.Shipped)]
    [InlineData(OrderStatusIds.Delivered)]
    public void Orders_in_progress_or_delivered_cannot_be_deleted(int statusId)
    {
        var order = TestData.OrderIn(statusId);
        Assert.Throws<DomainConflictException>(() => order.SoftDelete(TestData.UserId, TestData.Now));
        Assert.False(order.IsDeleted);
    }

    [Fact]
    public void A_deleted_order_rejects_every_change()
    {
        var order = TestData.PendingOrder();
        order.SoftDelete(TestData.UserId, TestData.Now);
        Assert.Throws<DomainConflictException>(() => order.Confirm(TestData.UserId, TestData.Now));
        Assert.Throws<DomainConflictException>(() => order.SoftDelete(TestData.UserId, TestData.Now));
        Assert.Throws<DomainConflictException>(() =>
            order.UpdateDetails(TestData.CustomerId, TestData.Customer(), "USD", null, new[] { TestData.Line() }, TestData.UserId, TestData.Now));
    }
}

public class PaymentBalanceTests
{
    [Fact]
    public void A_newer_balance_is_applied()
    {
        var order = TestData.OrderIn(OrderStatusIds.Confirmed);
        Assert.True(order.ApplyPaymentBalance(order.Total, 0, PaymentStatus.Paid, 1));
        Assert.Equal(order.Total, order.AmountPaid);
        Assert.Equal(0m, order.BalanceDue);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(1, order.PaymentBalanceVersion);
    }

    [Fact]
    public void A_stale_or_repeated_balance_is_ignored()
    {
        var order = TestData.OrderIn(OrderStatusIds.Confirmed);
        order.ApplyPaymentBalance(10m, order.Total - 10m, PaymentStatus.PartiallyPaid, 5);

        Assert.False(order.ApplyPaymentBalance(0m, order.Total, PaymentStatus.Unpaid, 4));
        Assert.False(order.ApplyPaymentBalance(0m, order.Total, PaymentStatus.Unpaid, 5));
        Assert.Equal(10m, order.AmountPaid);
        Assert.Equal(PaymentStatus.PartiallyPaid, order.PaymentStatus);
    }

    [Fact]
    public void Negative_amounts_are_rejected()
    {
        var order = TestData.OrderIn(OrderStatusIds.Confirmed);
        Assert.Throws<DomainValidationException>(() => order.ApplyPaymentBalance(-1m, 0, PaymentStatus.Paid, 1));
    }
}

public class OrderValueObjectTests
{
    [Theory]
    [InlineData("ord", "ORD")]
    [InlineData(" agr ", "AGR")]
    [InlineData("V2", "V2")]
    [InlineData("ABCDEFGHIJ", "ABCDEFGHIJ")]
    public void Prefix_is_normalized_to_upper_case(string input, string expected)
    {
        Assert.Equal(expected, OrderNumberPrefix.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1RD")]
    [InlineData("OR-D")]
    [InlineData("ABCDEFGHIJK")]
    public void Invalid_prefixes_are_rejected(string? input)
    {
        Assert.Throws<DomainValidationException>(() => OrderNumberPrefix.Normalize(input));
    }

    [Fact]
    public void Order_number_format_is_prefix_year_and_six_digits()
    {
        Assert.Equal("ORD-2026-000042", OrderNumberPrefix.Format("ORD", 2026, 42));
        Assert.Equal("ORD-2027-1234567", OrderNumberPrefix.Format("ORD", 2027, 1234567));
    }

    [Theory]
    [InlineData(1.50, 1)]
    [InlineData(1.234, 3)]
    [InlineData(10, 0)]
    public void Money_scale_ignores_trailing_zeros(decimal value, int scale)
    {
        Assert.Equal(scale, Money.Scale(value));
    }

    [Theory]
    [InlineData("X", null, null)]                      // name too short
    [InlineData("Valid Name", "12", null)]             // tax id too short
    [InlineData("Valid Name", null, "not-an-email")]   // bad email
    public void Invalid_customers_are_rejected(string name, string? taxId, string? email)
    {
        Assert.Throws<DomainValidationException>(() => new CustomerSnapshot(name, taxId, email, null));
    }

    [Fact]
    public void Blank_optional_customer_fields_become_null()
    {
        var customer = new CustomerSnapshot("  Finca Sol  ", " ", "", null);
        Assert.Equal("Finca Sol", customer.Name);
        Assert.Null(customer.TaxId);
        Assert.Null(customer.Email);
    }
}
