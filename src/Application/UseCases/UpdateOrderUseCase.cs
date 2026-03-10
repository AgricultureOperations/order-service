using Domain.Entities;
using Domain.Models;
using Domain.Ports.Driven;
using Domain.Ports.Driving;

namespace Application.UserCases;

public class UpdateOrderUseCase: IUpdateOrderUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public UpdateOrderUseCase(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;
    }

    public async Task<Order> Execute(Guid Id,UpdateOrderRequest Request)
    {
        var order = new Order(Request.customerId,Request.total);
        await _orderPersistencePort.UpdateOrder(Id,order);
        return order;
    }

}
