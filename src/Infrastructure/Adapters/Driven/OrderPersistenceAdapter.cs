using Microsoft.EntityFrameworkCore;
using Domain.Models;
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
    public Task Save(Order order)
    {
        _orderDbContext.Add(order);
        return _orderDbContext.SaveChangesAsync();
    }

    public async Task<Order?> GetById(Guid id)
    {
        return await _orderDbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
    }
}
