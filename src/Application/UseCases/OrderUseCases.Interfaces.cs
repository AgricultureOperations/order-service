using Application.DTOs;
using Domain.Entities;
using Domain.Ports.Driven;

namespace Application.UseCases;

// Inbound ports. One interface per operation; OrderController is the driving adapter.

public interface ICreateOrderUseCase
{
    Task<Order> Execute(CreateOrderRequest request, Guid? userId, string? idempotencyKey);
}

public interface IGetOrderByIdUseCase
{
    Task<Order?> Execute(Guid id);
}

public interface IListOrdersUseCase
{
    Task<PagedResult<Order>> Execute(OrderListQuery query);
}

/// <summary>Legacy v1 listing (unpaginated).</summary>
public interface IGetOrdersUseCase
{
    Task<IReadOnlyList<Order>> Execute();
}

public interface IUpdateOrderUseCase
{
    Task<Order> Execute(Guid id, UpdateOrderRequest request, Guid? userId);
}

public enum OrderTransition { Confirm, Ship, Deliver, Cancel }

public interface IChangeOrderStatusUseCase
{
    Task<Order> Execute(Guid id, OrderTransition transition, uint expectedVersion, string? reason, Guid? userId);
}

public interface IDeleteOrderUseCase
{
    Task Execute(Guid id, Guid? userId);
}

public interface IGetOrderStatusesUseCase
{
    Task<IReadOnlyList<OrderStatus>> Execute();
}

public interface IGetOrderSettingsUseCase
{
    Task<OrderSettingsResponse> Execute();
}

public interface IUpdateOrderSettingsUseCase
{
    Task<OrderSettingsResponse> Execute(UpdateOrderSettingsRequest request, Guid? userId);
}
