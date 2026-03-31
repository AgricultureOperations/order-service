using Domain.Entities;
using Domain.Ports.Driven;

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
