using Microsoft.EntityFrameworkCore;
using Domain.Models;

namespace Infrastructure.Persistence;

public class OrderDbContext: DbContext
{
    public OrderDbContext (DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
}
