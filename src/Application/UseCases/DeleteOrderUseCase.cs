using Domain.Ports.Driven;

namespace Application.UseCases;

/// <summary>Logical removal: the row stays in the database with IsDeleted = true.</summary>
public class DeleteOrderUseCase : IDeleteOrderUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;
    private readonly TimeProvider _clock;

    public DeleteOrderUseCase(IOrderPersistencePort orderPersistencePort, TimeProvider clock)
    {
        _orderPersistencePort = orderPersistencePort;
        _clock = clock;
    }

    public async Task Execute(Guid id, Guid? userId)
    {
        var order = await _orderPersistencePort.GetById(id) ?? throw new KeyNotFoundException("Order not found");
        order.SoftDelete(userId, _clock.GetUtcNow().UtcDateTime);
        await _orderPersistencePort.UpdateOrder(order, order.Version);
    }
}
