using Microsoft.EntityFrameworkCore;
using Domain.Entities;
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
    public async Task CreateOrder(Order order)
    {
        _orderDbContext.Add(order);
        await _orderDbContext.SaveChangesAsync();
    }

    public async Task<Order?> GetById(Guid id)
    {
        return await _orderDbContext.Orders
            .Include(o => o.Status)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<Order>?> GetAll()
    {
        return await _orderDbContext.Orders
            .Include(o => o.Status)
            .ToListAsync();
    }

    public async Task UpdateOrder(Order order)
    {
        _orderDbContext.Update(order);
        await _orderDbContext.SaveChangesAsync();
    }

    public async Task DeleteOrder(Order order)
    {
        _orderDbContext.Orders.Remove(order);
        await _orderDbContext.SaveChangesAsync();
        
    }
}
