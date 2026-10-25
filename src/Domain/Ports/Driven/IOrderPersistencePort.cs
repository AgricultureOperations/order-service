using Domain.Entities;

namespace Domain.Ports.Driven;

public interface IOrderPersistencePort
{
    /// <summary>Saves a new order, optionally recording the client's Idempotency-Key in the same transaction.
    /// Returns the stored order: the new one, or the one already created with that key by a concurrent request.</summary>
    Task<Order> CreateOrder(Order order, string? idempotencyKey);
    Task<Order?> GetByIdempotencyKey(string idempotencyKey);
    /// <summary>Loads a non-deleted order with its lines, tracked for changes.</summary>
    Task<Order?> GetById(Guid id);
    Task<PagedResult<Order>> List(OrderListQuery query);
    /// <summary>Legacy v1 listing: every non-deleted order, without lines.</summary>
    Task<IReadOnlyList<Order>> GetAll();
    /// <summary>Persists changes to a tracked order. <paramref name="expectedVersion"/> is the version the client
    /// read; a mismatch with the stored row throws <see cref="Exceptions.ConcurrencyConflictException"/>.</summary>
    Task UpdateOrder(Order order, uint expectedVersion);
    Task<IReadOnlyList<OrderStatus>> GetStatuses();
}
