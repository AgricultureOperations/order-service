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

    public async Task<Order> Execute(Guid Id)
    {
        return await _orderPersistencePort.DeleteOrder(Id);;
    }
}
