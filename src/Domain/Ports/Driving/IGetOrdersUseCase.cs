using Domain.Models;

namespace Domain.Ports.Driving;

public interface IGetOrdersUseCase
{
    Task<IEnumerable<Order>?> Execute();
}
