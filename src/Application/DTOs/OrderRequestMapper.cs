using Domain.Entities;
using Domain.Exceptions;

namespace Application.DTOs;

internal static class OrderRequestMapper
{
    public static (Guid Id, CustomerSnapshot Snapshot) ToCustomer(this CreateOrderRequest request)
    {
        var c = request.Customer ?? throw new DomainValidationException("Customer is required.");
        if (c.Id == Guid.Empty) throw new DomainValidationException("Select the customer (an auth-service customer).");
        return (c.Id, new CustomerSnapshot(c.Name, c.TaxId, c.Email, c.Phone));
    }

    public static List<OrderLine> ToLines(this CreateOrderRequest request) =>
        (request.Lines ?? new List<OrderLineRequest>())
            .Select(l => new OrderLine(l.ProductId, l.Sku ?? string.Empty, l.ProductName ?? string.Empty, l.Unit ?? string.Empty,
                l.Quantity, l.UnitPrice, l.DiscountAmount, l.TaxRate))
            .ToList();
}
