using Api.Tests.Support;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Api.Tests;

/// <summary>Applies the migrations to a database that holds pre-v2 rows, the way a Render deploy would.</summary>
[Collection("api")]
public class MigrationTests
{
    private readonly ApiFactory _factory;
    public MigrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OrderMaintainerV2_backfills_legacy_orders()
    {
        var connection = new NpgsqlConnectionStringBuilder(_factory.ConnectionString) { Database = $"legacy_{Guid.NewGuid():N}" }.ConnectionString;
        var options = new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(connection).Options;

        await using (var db = new OrderDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20260407202146_InitialCreate");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Orders" ("Id", "CustomerId", total, "CreatedAt", "StatusId") VALUES
                ('00000000-0000-0000-0000-00000000000a', '11111111-1111-1111-1111-111111111111', 120.456, '2025-03-01T10:00:00Z', 1),
                ('00000000-0000-0000-0000-00000000000b', '11111111-1111-1111-1111-111111111111', 50,      '2026-01-15T10:00:00Z', 2),
                ('00000000-0000-0000-0000-00000000000c', '11111111-1111-1111-1111-111111111111', 10,      '2026-02-01T10:00:00Z', 5);
                """);

            await db.Database.MigrateAsync();
        }

        await using (var db = new OrderDbContext(options))
        {
            var orders = await db.Orders.AsNoTracking().OrderBy(o => o.CreatedAt).ToListAsync();
            // Numbered per year from 1, oldest first.
            Assert.Equal(new[] { "ORD-2025-000001", "ORD-2026-000001", "ORD-2026-000002" }, orders.Select(o => o.OrderNumber));

            var pending = orders[0];
            Assert.Equal(OrderStatusIds.Pending, pending.StatusId);
            Assert.Equal(120.456m, pending.Total);  // the legacy column keeps its precision
            Assert.Equal(120.46m, pending.Subtotal);
            Assert.Equal(120.46m, pending.BalanceDue);
            Assert.Equal(PaymentStatus.Unpaid, pending.PaymentStatus);
            Assert.Equal("USD", pending.Currency);
            Assert.False(pending.IsDeleted);

            var paid = orders[1];
            Assert.Equal(OrderStatusIds.Confirmed, paid.StatusId);
            Assert.Equal(PaymentStatus.Paid, paid.PaymentStatus);
            Assert.Equal(50m, paid.AmountPaid);
            Assert.Equal(0m, paid.BalanceDue);
            Assert.Equal(paid.CreatedAt, paid.ConfirmedAt);

            Assert.Equal(OrderStatusIds.Cancelled, orders[2].StatusId);

            // Each year's counter starts after its backfilled orders.
            var counters = await db.Set<OrderNumberCounter>().AsNoTracking().OrderBy(c => c.Year).ToListAsync();
            Assert.Equal(new[] { ("ORD", 2025, 1L), ("ORD", 2026, 2L) }, counters.Select(c => (c.Prefix, c.Year, c.LastValue)));

            Assert.Equal("ORD", (await db.Settings.SingleAsync(s => s.Key == SettingKeys.OrderNumberPrefix)).Value);
        }
    }

    [Fact]
    public async Task A_pre_v2_insert_still_works_after_the_migration()
    {
        // The old container keeps serving during a deploy and inserts only the original columns.
        await _factory.WithDb(async db =>
        {
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Orders\" (\"Id\", \"CustomerId\", total, \"CreatedAt\", \"StatusId\") VALUES ({0}, {1}, 10, now(), 1)",
                id, Guid.NewGuid());
            var row = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
            Assert.Equal(PaymentStatus.Unpaid, row.PaymentStatus);
            Assert.Equal("USD", row.Currency);
            Assert.False(row.IsDeleted);
            Assert.Null(row.OrderNumber);
        });
    }
}
