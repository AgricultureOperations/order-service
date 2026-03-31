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
        return await _orderDbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<Order>?> GetAll()
    {
        return await _orderDbContext.Orders.ToListAsync();
    }

    public async Task UpdateOrder(Guid id,Order order)
    {
        var existingOrder = await _orderDbContext.Orders.FirstOrDefaultAsync(e => e.Id == id);
        if( existingOrder == null )
            return;

        existingOrder.UpdateOrder(order.CustomerId,order.total);
        
        _orderDbContext.Update(existingOrder);
        await _orderDbContext.SaveChangesAsync();
    }

    public async Task<Order> DeleteOrder(Guid id)
    {
        var order = await _orderDbContext.Orders.FirstOrDefaultAsync(e => e.Id == id);
        if( order == null )
            throw new KeyNotFoundException("Order not found");
        
        _orderDbContext.Orders.Remove(order);
        await _orderDbContext.SaveChangesAsync();
        return order;
    }
}
