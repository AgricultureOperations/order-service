using Application.DTOs;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class UpdateOrderUseCase : IUpdateOrderUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;
    private readonly TimeProvider _clock;

    public UpdateOrderUseCase(IOrderPersistencePort orderPersistencePort, TimeProvider clock)
    {
        _orderPersistencePort = orderPersistencePort;
        _clock = clock;
    }

    public async Task<Order> Execute(Guid id, UpdateOrderRequest request, Guid? userId)
    {
        var order = await _orderPersistencePort.GetById(id) ?? throw new KeyNotFoundException("Order not found");
        if (order.Version != request.Version) throw new ConcurrencyConflictException();

        var (customerId, customer) = request.ToCustomer();
        order.UpdateDetails(customerId, customer, request.Currency, request.Notes, request.ToLines(),
            userId, _clock.GetUtcNow().UtcDateTime);
        await _orderPersistencePort.UpdateOrder(order, request.Version);
        return order;
    }
}
