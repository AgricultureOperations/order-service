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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderStatus>().HasData(
            new OrderStatus { Id = 1, Name = "Pending" },
            new OrderStatus { Id = 2, Name = "Paid" },
            new OrderStatus { Id = 3, Name = "Shipped" },
            new OrderStatus { Id = 4, Name = "Delivered" },
            new OrderStatus { Id = 5, Name = "Cancelled" }
        );
    }
}
