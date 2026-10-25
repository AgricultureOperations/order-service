namespace Application.DTOs;

// Request contracts for /api/v2/orders. Property names are camelCase on the wire (ASP.NET default).
// Totals are never accepted from the client: the Order aggregate calculates them from the lines.

/// <summary>The buyer as read from auth-service's /api/v1/customers: its id plus a snapshot of its data.</summary>
public class CustomerRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    /// <summary>Cédula, RUC or passport.</summary>
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class OrderLineRequest
{
    public Guid ProductId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Price snapshot taken by the client from product-service. Validated, not trusted for totals.</summary>
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>0 to 1 (0.15 = 15%).</summary>
    public decimal TaxRate { get; set; }
}

public class CreateOrderRequest
{
    public CustomerRequest? Customer { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public List<OrderLineRequest>? Lines { get; set; }
}

public class UpdateOrderRequest : CreateOrderRequest
{
    /// <summary>The <c>version</c> the client read. A stale value returns 409.</summary>
    public uint Version { get; set; }
}

public class OrderTransitionRequest
{
    public uint Version { get; set; }
}

public class CancelOrderRequest : OrderTransitionRequest
{
    public string? Reason { get; set; }
}

public class UpdateOrderSettingsRequest
{
    public string? OrderNumberPrefix { get; set; }
}
