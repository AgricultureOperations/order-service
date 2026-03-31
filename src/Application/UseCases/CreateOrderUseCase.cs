using Application.DTOs;
using Domain.Entities;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class CreateOrderUseCase: ICreateOrderUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public CreateOrderUseCase(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;       
    }

    public async Task<Order> Execute(CreateOrderRequest Request)
    {
        var order = new Order(Request.customerId,Request.total);
        await _orderPersistencePort.CreateOrder(order); 
        return order;
    }
}
