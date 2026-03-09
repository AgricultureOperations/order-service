using Domain.Models;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class CreateOrder
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public CreateOrder(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;       
    }

    public async Task<Order> Execute(Guid customerId, decimal total)
    {
        var order = new Order(customerId,total);
        await _orderPersistencePort.Save(order);
        return order;   
    }
}
