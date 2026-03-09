using Domain.Models;

namespace Domain.Ports.Driving;

public interface IGetOrderByIdUseCase
{
    Task<Order?> Execute(Guid id);
}
