using Application.DTOs;
using Domain.Entities;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class UpdateOrderUseCase: IUpdateOrderUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public UpdateOrderUseCase(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;
    }

    public async Task Execute(Guid Id,UpdateOrderRequest request)
    {
        var order = await _orderPersistencePort.GetById(Id);
        if ( order == null ) throw new KeyNotFoundException("Order not found"); 
        
        order.UpdateTotal(request.total);
        await _orderPersistencePort.UpdateOrder(order);
    }

}
