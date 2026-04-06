using Domain.Entities;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class DeleteOrderUseCase: IDeleteOrderUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;
    public DeleteOrderUseCase(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;
    }

    public async Task Execute(Guid Id)
    {
        var order = await _orderPersistencePort.GetById(Id);
        if ( order == null ) throw new KeyNotFoundException("Order not found");
        await _orderPersistencePort.DeleteOrder(order);;
    }
}
