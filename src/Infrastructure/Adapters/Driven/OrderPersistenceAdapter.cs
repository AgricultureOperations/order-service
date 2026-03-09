using Domain.Models;
using Domain.Ports.Driven;

namespace Infrastructure.Adapters.Driven;

public class OrderPersistenceAdapter: IOrderPersistencePort
{
    private readonly List<Order> _order = new();

    public Task Save(Order order)
    {
        this._order.Add(order);
        return Task.CompletedTask;
    }
}
