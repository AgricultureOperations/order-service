using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Ports.Driven;
using Infrastructure.Persistence;

namespace Infrastructure.Adapters.Driven;

/// <summary>
/// Builds <c>{prefix}-{yyyy}-{000000}</c> from the <c>orders.number.prefix</c> setting and the
/// <c>OrderNumberCounters</c> row for that prefix and year. The upsert takes a row lock that is held until the
/// surrounding transaction ends, so concurrent creates (even across containers) queue on it and never share a
/// number; a rollback undoes the increment, so numbers have no gaps.
/// </summary>
public class OrderNumberGeneratorAdapter : IOrderNumberGeneratorPort
{
    private readonly OrderDbContext _orderDbContext;

    public OrderNumberGeneratorAdapter(OrderDbContext orderDbContext)
    {
        _orderDbContext = orderDbContext;
    }

    public async Task<string> Next(int year)
    {
        if (_orderDbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Order numbers must be taken inside IUnitOfWorkPort.InTransaction.");

        var prefix = await CurrentPrefix();
        // ToListAsync, not SingleAsync: EF must not wrap the INSERT in a subquery.
        var values = await _orderDbContext.Database.SqlQuery<long>($"""
            INSERT INTO "OrderNumberCounters" ("Prefix", "Year", "LastValue") VALUES ({prefix}, {year}, 1)
            ON CONFLICT ("Prefix", "Year") DO UPDATE SET "LastValue" = "OrderNumberCounters"."LastValue" + 1
            RETURNING "LastValue" AS "Value"
            """).ToListAsync();

        return OrderNumberPrefix.Format(prefix, year, values.Single());
    }

    public async Task<string> Peek(string prefix, int year)
    {
        var last = await _orderDbContext.Set<OrderNumberCounter>().AsNoTracking()
            .Where(c => c.Prefix == prefix && c.Year == year)
            .Select(c => (long?)c.LastValue)
            .FirstOrDefaultAsync() ?? 0;
        return OrderNumberPrefix.Format(prefix, year, last + 1);
    }

    private async Task<string> CurrentPrefix() =>
        await _orderDbContext.Settings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.OrderNumberPrefix)
            .Select(s => s.Value)
            .FirstOrDefaultAsync() ?? OrderNumberPrefix.Default;
}
