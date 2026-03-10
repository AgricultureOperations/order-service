using Domain.Models;
using Domain.Ports.Driven;
using Domain.Ports.Driving;

namespace Application.UseCases;

public class GetOrdersUseCase: IGetOrdersUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;
    
    public GetOrdersUseCase(IOrderPersistencePort orderPersistencePort)
    {
        this._orderPersistencePort = orderPersistencePort;
    }

    public async Task<IEnumerable<Order>?> Execute()
    {
        return await _orderPersistencePort.GetAll();
    }
}
