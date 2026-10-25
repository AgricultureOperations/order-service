using Domain.Entities;
using Domain.Exceptions;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class ChangeOrderStatusUseCase : IChangeOrderStatusUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;
    private readonly TimeProvider _clock;

    public ChangeOrderStatusUseCase(IOrderPersistencePort orderPersistencePort, TimeProvider clock)
    {
        _orderPersistencePort = orderPersistencePort;
        _clock = clock;
    }

    public async Task<Order> Execute(Guid id, OrderTransition transition, uint expectedVersion, string? reason, Guid? userId)
    {
        var order = await _orderPersistencePort.GetById(id) ?? throw new KeyNotFoundException("Order not found");
        if (order.Version != expectedVersion) throw new ConcurrencyConflictException();

        var now = _clock.GetUtcNow().UtcDateTime;
        switch (transition)
        {
            case OrderTransition.Confirm: order.Confirm(userId, now); break;
            case OrderTransition.Ship: order.Ship(userId, now); break;
            case OrderTransition.Deliver: order.Deliver(userId, now); break;
            case OrderTransition.Cancel: order.Cancel(reason, userId, now); break;
            default: throw new ArgumentOutOfRangeException(nameof(transition), transition, null);
        }

        await _orderPersistencePort.UpdateOrder(order, expectedVersion);
        return order;
    }
}
