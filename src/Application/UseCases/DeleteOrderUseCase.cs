using Domain.Entities;
using Domain.Models;
using Domain.Ports.Driven;
using Domain.Ports.Driving;

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
