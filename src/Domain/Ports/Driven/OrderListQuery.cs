using Domain.Entities;

namespace Domain.Ports.Driven;

public enum OrderSortField { CreatedAt, OrderNumber, Total, CustomerName }

public sealed record OrderListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    /// <summary>Case-insensitive match on order number, customer name or tax ID.</summary>
    public string? Search { get; init; }
    public int? StatusId { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    /// <summary>Inclusive lower bound on CreatedAt.</summary>
    public DateTime? From { get; init; }
    /// <summary>Exclusive upper bound on CreatedAt.</summary>
    public DateTime? To { get; init; }
    public OrderSortField Sort { get; init; } = OrderSortField.CreatedAt;
    public bool Descending { get; init; } = true;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
