using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Infrastructure.Persistence;

public class OrderDbContext: DbContext
{
    public OrderDbContext (DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Table name predates the DbSet; keep it so the running pre-v2 container never loses its table.
        modelBuilder.Entity<OrderStatus>().ToTable("OrderStatus");
        modelBuilder.Entity<OrderStatus>().HasData(
            new OrderStatus { Id = 1, Name = "Pending" },
            new OrderStatus { Id = 2, Name = "Paid" },
            new OrderStatus { Id = 3, Name = "Shipped" },
            new OrderStatus { Id = 4, Name = "Delivered" },
            new OrderStatus { Id = 5, Name = "Cancelled" },
            new OrderStatus { Id = 6, Name = "Confirmed" }
        );

        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).ValueGeneratedNever();

            // xmin is PostgreSQL's row version: any UPDATE changes it, so a stale client write fails.
            order.Property(o => o.Version).IsRowVersion();
            order.HasQueryFilter(o => !o.IsDeleted);

            order.Property(o => o.OrderNumber).HasMaxLength(32);
            order.HasIndex(o => o.OrderNumber).IsUnique();
            order.HasIndex(o => o.CreatedAt);

            order.Property(o => o.CustomerName).HasMaxLength(200);
            order.Property(o => o.CustomerTaxId).HasMaxLength(20);
            order.Property(o => o.CustomerEmail).HasMaxLength(254);
            order.Property(o => o.CustomerPhone).HasMaxLength(30);
            order.Property(o => o.Currency).HasMaxLength(3).HasDefaultValue(Order.DefaultCurrency);
            order.Property(o => o.Notes).HasMaxLength(1000);
            order.Property(o => o.CancelReason).HasMaxLength(500);

            // The pre-v2 column is "total" with an unbounded numeric type. It keeps both: renaming or narrowing
            // it would break the container that is still running during a deploy (expand/contract).
            order.Property(o => o.Total).HasColumnName("total").HasColumnType("numeric");
            order.Property(o => o.Subtotal).HasPrecision(18, 2);
            order.Property(o => o.DiscountTotal).HasPrecision(18, 2);
            order.Property(o => o.TaxTotal).HasPrecision(18, 2);
            order.Property(o => o.AmountPaid).HasPrecision(18, 2);
            order.Property(o => o.BalanceDue).HasPrecision(18, 2);
            order.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(PaymentStatus.Unpaid);
            order.Property(o => o.IsDeleted).HasDefaultValue(false);

            order.HasOne(o => o.Status).WithMany().HasForeignKey(o => o.StatusId);

            order.OwnsMany(o => o.Lines, line =>
            {
                line.ToTable("OrderLines");
                line.WithOwner().HasForeignKey("OrderId");
                line.HasKey(l => l.Id);
                line.Property(l => l.Id).ValueGeneratedNever();
                line.Property(l => l.Sku).HasMaxLength(64);
                line.Property(l => l.ProductName).HasMaxLength(200);
                line.Property(l => l.Unit).HasMaxLength(32);
                line.Property(l => l.Quantity).HasPrecision(18, 3);
                line.Property(l => l.UnitPrice).HasPrecision(18, 2);
                line.Property(l => l.DiscountAmount).HasPrecision(18, 2);
                line.Property(l => l.TaxRate).HasPrecision(5, 4);
                line.Property(l => l.LineGross).HasPrecision(18, 2);
                line.Property(l => l.LineSubtotal).HasPrecision(18, 2);
                line.Property(l => l.LineTax).HasPrecision(18, 2);
                line.Property(l => l.LineTotal).HasPrecision(18, 2);
                line.HasIndex(l => l.ProductId);
            });
            order.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Setting>(setting =>
        {
            setting.HasKey(s => s.Key);
            setting.Property(s => s.Key).HasMaxLength(100);
            setting.Property(s => s.Value).HasMaxLength(500);
            setting.HasData(new
            {
                Key = SettingKeys.OrderNumberPrefix,
                Value = OrderNumberPrefix.Default,
                UpdatedAt = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc),
                UpdatedBy = (Guid?)null,
            });
        });

        modelBuilder.Entity<OrderNumberCounter>(counter =>
        {
            counter.ToTable("OrderNumberCounters");
            counter.HasKey(c => new { c.Prefix, c.Year });
            counter.Property(c => c.Prefix).HasMaxLength(10);
        });

        modelBuilder.Entity<IdempotencyRecord>(record =>
        {
            record.HasKey(r => r.Key);
            record.Property(r => r.Key).HasMaxLength(100);
            record.HasOne<Order>().WithMany().HasForeignKey(r => r.OrderId);
        });
    }
}
