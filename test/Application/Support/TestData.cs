using Application.DTOs;
using Domain.Entities;
using Domain.Ports.Driven;

namespace Application.Tests.Support;

public sealed class FixedClock : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FixedClock(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>Runs the work directly: the unit tests mock the adapters, so there is no transaction to open.</summary>
public sealed class PassThroughUnitOfWork : IUnitOfWorkPort
{
    public int Calls { get; private set; }
    public Task<T> InTransaction<T>(Func<Task<T>> work)
    {
        Calls++;
        return work();
    }
}

public static class TestData
{
    public static readonly DateTime Now = new(2026, 10, 24, 15, 0, 0, DateTimeKind.Utc);
    public static readonly FixedClock Clock = new(new DateTimeOffset(Now));
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CustomerId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    public static CustomerSnapshot Customer(string name = "Hacienda La Esperanza") =>
        new(name, "0991234567001", "compras@esperanza.ec", "+593 99 123 4567");

    public static OrderLine Line(decimal quantity = 2, decimal unitPrice = 10m, decimal discount = 0, decimal taxRate = 0.15m, string sku = "FERT-001") =>
        new(Guid.NewGuid(), sku, "Urea 46%", "bag", quantity, unitPrice, discount, taxRate);

    public static Order PendingOrder(params OrderLine[] lines) =>
        Order.Create("ORD-2026-000001", CustomerId, Customer(), "USD", null, lines.Length == 0 ? new[] { Line() } : lines, UserId, Now);

    public static Order OrderIn(int statusId)
    {
        var order = PendingOrder();
        if (statusId == OrderStatusIds.Pending) return order;
        if (statusId == OrderStatusIds.Cancelled) { order.Cancel("Customer changed their mind", UserId, Now); return order; }
        order.Confirm(UserId, Now);
        if (statusId == OrderStatusIds.Confirmed) return order;
        order.Ship(UserId, Now);
        if (statusId == OrderStatusIds.Shipped) return order;
        order.Deliver(UserId, Now);
        return order;
    }

    public static CreateOrderRequest CreateRequest() => new()
    {
        Customer = new CustomerRequest { Id = CustomerId, Name = "Hacienda La Esperanza", TaxId = "0991234567001" },
        Currency = "usd",
        Lines = new List<OrderLineRequest>
        {
            new() { ProductId = Guid.NewGuid(), Sku = "FERT-001", ProductName = "Urea 46%", Unit = "bag", Quantity = 3, UnitPrice = 25.50m, TaxRate = 0.15m },
        },
    };
}
