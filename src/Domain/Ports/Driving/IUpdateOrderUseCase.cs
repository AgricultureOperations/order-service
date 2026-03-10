using Domain.Entities;
using Domain.Models;

namespace Domain.Ports.Driving;

public interface IUpdateOrderUseCase
{
    Task<Order> Execute(Guid Id, UpdateOrderRequest Request);
}
