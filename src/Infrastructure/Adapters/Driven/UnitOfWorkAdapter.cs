using Domain.Ports.Driven;
using Infrastructure.Persistence;

namespace Infrastructure.Adapters.Driven;

/// <summary>One EF transaction on the request's scoped OrderDbContext, so every adapter of the request takes part.</summary>
public class UnitOfWorkAdapter : IUnitOfWorkPort
{
    private readonly OrderDbContext _orderDbContext;

    public UnitOfWorkAdapter(OrderDbContext orderDbContext)
    {
        _orderDbContext = orderDbContext;
    }

    public async Task<T> InTransaction<T>(Func<Task<T>> work)
    {
        // Nested calls join the outer transaction.
        if (_orderDbContext.Database.CurrentTransaction is not null) return await work();

        await using var transaction = await _orderDbContext.Database.BeginTransactionAsync();
        try
        {
            var result = await work();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            // Entities from the failed attempt must not leak into later queries of this request.
            _orderDbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
