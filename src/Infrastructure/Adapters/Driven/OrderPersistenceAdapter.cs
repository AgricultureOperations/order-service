using Microsoft.EntityFrameworkCore;
using Npgsql;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Ports.Driven;
using Infrastructure.Persistence;

namespace Infrastructure.Adapters.Driven;

public class OrderPersistenceAdapter: IOrderPersistencePort
{
    private readonly OrderDbContext _orderDbContext;

    public OrderPersistenceAdapter(OrderDbContext orderDbContext)
    {
        this._orderDbContext = orderDbContext;
    }

    public async Task<Order> CreateOrder(Order order, string? idempotencyKey)
    {
        _orderDbContext.Orders.Add(order);
        if (idempotencyKey is not null)
        {
            _orderDbContext.IdempotencyKeys.Add(new IdempotencyRecord
            {
                Key = idempotencyKey, OrderId = order.Id, UserId = order.CreatedBy, CreatedAt = order.CreatedAt,
            });
        }

        try
        {
            await _orderDbContext.SaveChangesAsync();
            return order;
        }
        catch (DbUpdateException ex) when (idempotencyKey is not null && IsIdempotencyKeyViolation(ex))
        {
            // A concurrent request with the same key won the race. The transaction is now aborted, so the use case
            // rolls back and looks that order up afterwards.
            throw new IdempotencyKeyConflictException();
        }
    }

    public async Task<Order?> GetByIdempotencyKey(string idempotencyKey)
    {
        var orderId = await _orderDbContext.IdempotencyKeys.AsNoTracking()
            .Where(r => r.Key == idempotencyKey)
            .Select(r => (Guid?)r.OrderId)
            .FirstOrDefaultAsync();
        return orderId is null ? null : await GetById(orderId.Value);
    }

    public Task<Order?> GetById(Guid id) =>
        _orderDbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);

    public async Task<PagedResult<Order>> List(OrderListQuery query)
    {
        var orders = _orderDbContext.Orders.AsNoTracking();

        if (query.Search is { } search)
        {
            var pattern = $"%{EscapeLike(search)}%";
            orders = orders.Where(o =>
                EF.Functions.ILike(o.OrderNumber!, pattern) ||
                EF.Functions.ILike(o.CustomerName!, pattern) ||
                EF.Functions.ILike(o.CustomerTaxId!, pattern));
        }
        if (query.StatusId is { } statusId) orders = orders.Where(o => o.StatusId == statusId);
        if (query.PaymentStatus is { } paymentStatus) orders = orders.Where(o => o.PaymentStatus == paymentStatus);
        if (query.From is { } from) orders = orders.Where(o => o.CreatedAt >= from);
        if (query.To is { } to) orders = orders.Where(o => o.CreatedAt < to);

        var total = await orders.CountAsync();

        orders = (query.Sort, query.Descending) switch
        {
            (OrderSortField.OrderNumber, false) => orders.OrderBy(o => o.OrderNumber).ThenBy(o => o.Id),
            (OrderSortField.OrderNumber, true) => orders.OrderByDescending(o => o.OrderNumber).ThenBy(o => o.Id),
            (OrderSortField.Total, false) => orders.OrderBy(o => o.Total).ThenBy(o => o.Id),
            (OrderSortField.Total, true) => orders.OrderByDescending(o => o.Total).ThenBy(o => o.Id),
            (OrderSortField.CustomerName, false) => orders.OrderBy(o => o.CustomerName).ThenBy(o => o.Id),
            (OrderSortField.CustomerName, true) => orders.OrderByDescending(o => o.CustomerName).ThenBy(o => o.Id),
            (_, false) => orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id),
            (_, true) => orders.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id),
        };

        var items = await orders.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        return new PagedResult<Order>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<Order>> GetAll() =>
        await _orderDbContext.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).ToListAsync();

    public async Task UpdateOrder(Order order, uint expectedVersion)
    {
        _orderDbContext.Entry(order).Property(o => o.Version).OriginalValue = expectedVersion;
        try
        {
            await _orderDbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }

    public async Task<IReadOnlyList<OrderStatus>> GetStatuses() =>
        await _orderDbContext.OrderStatuses.AsNoTracking().OrderBy(s => s.Id).ToListAsync();

    private static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == "PK_IdempotencyKeys";

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
