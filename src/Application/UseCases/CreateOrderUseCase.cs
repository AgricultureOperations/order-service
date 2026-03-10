using Domain.Models;
using Domain.Ports.Driven;
using Domain.Ports.Driving;

namespace Application.UseCases;

public class CreateOrderUseCase: ICreateOrderUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public CreateOrderUseCase(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;       
    }

    public async Task<Order> Execute(Guid customerId, decimal total)
    {
        var order = new Order(customerId,total);
        await _orderPersistencePort.CreateOrder(order); 
        return order;
    }
}
