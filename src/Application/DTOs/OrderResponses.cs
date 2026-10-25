using Domain.Entities;
using Domain.Ports.Driven;

namespace Application.DTOs;

public record OrderStatusResponse(int Id, string Name);

/// <summary>The buyer: <c>Id</c> is the auth-service customer (null on pre-v2 orders), the rest a snapshot.</summary>
public record CustomerResponse(Guid? Id, string? Name, string? TaxId, string? Email, string? Phone);

public record OrderLineResponse(
    Guid Id, Guid ProductId, string Sku, string ProductName, string Unit,
    decimal Quantity, decimal UnitPrice, decimal DiscountAmount, decimal TaxRate,
    decimal LineGross, decimal LineSubtotal, decimal LineTax, decimal LineTotal);

/// <summary>List row: the order without its lines.</summary>
public record OrderSummaryResponse(
    Guid Id, string? OrderNumber, CustomerResponse Customer, string Currency, int ItemCount,
    decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal Total,
    decimal AmountPaid, decimal BalanceDue, string PaymentStatus,
    int StatusId, OrderStatusResponse Status,
    DateTime CreatedAt, DateTime? UpdatedAt, uint Version);

public record OrderResponse(
    Guid Id, string? OrderNumber, CustomerResponse Customer, string Currency, string? Notes,
    IReadOnlyList<OrderLineResponse> Lines, int ItemCount,
    decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal Total,
    decimal AmountPaid, decimal BalanceDue, string PaymentStatus,
    int StatusId, OrderStatusResponse Status, string? CancelReason,
    DateTime? ConfirmedAt, DateTime? ShippedAt, DateTime? DeliveredAt, DateTime? CancelledAt,
    DateTime CreatedAt, Guid? CreatedBy, DateTime? UpdatedAt, Guid? UpdatedBy, uint Version);

/// <summary>Shape served by the deprecated <c>GET /api/v1/orders</c> routes, unchanged for old clients.</summary>
public record LegacyOrderResponse(Guid Id, Guid? CustomerId, decimal Total, DateTime CreatedAt, int StatusId, OrderStatusResponse Status);

public record PageMeta(int Page, int PageSize, int Total, int TotalPages);

/// <summary><c>{ data, meta }</c>, the same paginated shape as product-service and auth-service.</summary>
public record PagedResponse<T>(IReadOnlyList<T> Data, PageMeta Meta);

/// <summary><c>NextNumber</c> is what the next order gets with the current prefix this year (not reserved).</summary>
public record OrderSettingsResponse(string OrderNumberPrefix, string NextNumber, DateTime UpdatedAt, Guid? UpdatedBy);

public static class OrderResponseMapper
{
    public static OrderStatusResponse Status(int statusId) => new(statusId, OrderStatusNames.Of(statusId));

    private static CustomerResponse Customer(Order o) => new(o.CustomerId, o.CustomerName, o.CustomerTaxId, o.CustomerEmail, o.CustomerPhone);

    public static OrderResponse ToResponse(this Order o) => new(
        o.Id, o.OrderNumber, Customer(o), o.Currency, o.Notes,
        o.Lines.Select(l => new OrderLineResponse(
            l.Id, l.ProductId, l.Sku, l.ProductName, l.Unit, l.Quantity, l.UnitPrice, l.DiscountAmount, l.TaxRate,
            l.LineGross, l.LineSubtotal, l.LineTax, l.LineTotal)).ToList(),
        o.Lines.Count,
        o.Subtotal, o.DiscountTotal, o.TaxTotal, o.Total,
        o.AmountPaid, o.BalanceDue, o.PaymentStatus.ToString(),
        o.StatusId, Status(o.StatusId), o.CancelReason,
        o.ConfirmedAt, o.ShippedAt, o.DeliveredAt, o.CancelledAt,
        o.CreatedAt, o.CreatedBy, o.UpdatedAt, o.UpdatedBy, o.Version);

    public static OrderSummaryResponse ToSummary(this Order o) => new(
        o.Id, o.OrderNumber, Customer(o), o.Currency, o.Lines.Count,
        o.Subtotal, o.DiscountTotal, o.TaxTotal, o.Total,
        o.AmountPaid, o.BalanceDue, o.PaymentStatus.ToString(),
        o.StatusId, Status(o.StatusId), o.CreatedAt, o.UpdatedAt, o.Version);

    public static LegacyOrderResponse ToLegacy(this Order o) =>
        new(o.Id, o.CustomerId, o.Total, o.CreatedAt, o.StatusId, Status(o.StatusId));

    public static PagedResponse<OrderSummaryResponse> ToResponse(this PagedResult<Order> page) => new(
        page.Items.Select(ToSummary).ToList(),
        new PageMeta(page.Page, page.PageSize, page.Total, page.TotalPages));
}
