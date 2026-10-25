using Domain.Entities;
using Domain.Exceptions;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class GetOrderByIdUseCase : IGetOrderByIdUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public GetOrderByIdUseCase(IOrderPersistencePort orderPersistencePort)
    {
        _orderPersistencePort = orderPersistencePort;
    }

    public Task<Order?> Execute(Guid id) => _orderPersistencePort.GetById(id);
}

public class ListOrdersUseCase : IListOrdersUseCase
{
    public const int MaxPageSize = 100;

    private readonly IOrderPersistencePort _orderPersistencePort;

    public ListOrdersUseCase(IOrderPersistencePort orderPersistencePort)
    {
        _orderPersistencePort = orderPersistencePort;
    }

    public Task<PagedResult<Order>> Execute(OrderListQuery query)
    {
        if (query.Page < 1) throw new DomainValidationException("page must be 1 or greater.");
        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
            throw new DomainValidationException($"pageSize must be between 1 and {MaxPageSize}.");
        if (query.From is not null && query.To is not null && query.From >= query.To)
            throw new DomainValidationException("from must be earlier than to.");
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        return _orderPersistencePort.List(query with { Search = search });
    }
}

public class GetOrdersUseCase : IGetOrdersUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public GetOrdersUseCase(IOrderPersistencePort orderPersistencePort)
    {
        _orderPersistencePort = orderPersistencePort;
    }

    public Task<IReadOnlyList<Order>> Execute() => _orderPersistencePort.GetAll();
}

public class GetOrderStatusesUseCase : IGetOrderStatusesUseCase
{
    private readonly IOrderPersistencePort _orderPersistencePort;

    public GetOrderStatusesUseCase(IOrderPersistencePort orderPersistencePort)
    {
        _orderPersistencePort = orderPersistencePort;
    }

    public Task<IReadOnlyList<OrderStatus>> Execute() => _orderPersistencePort.GetStatuses();
}
