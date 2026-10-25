namespace Domain.Entities;

public class OrderStatus
{
    public int Id { get; set;}
    public string Name { get; set;}
    public OrderStatus() {}
}

/// <summary>
/// Ids of the seeded <see cref="OrderStatus"/> rows. Append only: ids are stored in Orders.StatusId
/// and mirrored by the frontend, so never renumber or reuse one.
/// </summary>
public static class OrderStatusIds
{
    public const int Pending = 1;
    /// <summary>Legacy. Payment state now lives in <see cref="PaymentStatus"/>; no transition leads here.</summary>
    public const int Paid = 2;
    public const int Shipped = 3;
    public const int Delivered = 4;
    public const int Cancelled = 5;
    public const int Confirmed = 6;
}
