using Domain.Models;
using Domain.Ports.Driven;
using Microsoft.VisualBasic;

namespace Application.UseCases;

public class GetByIdOrderUseCase
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
