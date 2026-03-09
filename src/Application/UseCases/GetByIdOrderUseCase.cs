using Domain.Models;
using Domain.Ports.Driven;
using Domain.Ports.Driving;

namespace Application.UseCases;

public class GetByIdOrderUseCase: IGetOrderByIdUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public GetByIdOrderUseCase(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;       
    }

    public async Task<Order?> Execute(Guid id)
    {
        return await _orderPersistencePort.GetById(id);   
    }
}
