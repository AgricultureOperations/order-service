using Domain.Models;

namespace Domain.Ports.Driving;

public interface IDeleteOrderUseCase
{
    Task<Order> Execute(Guid Id);
}
