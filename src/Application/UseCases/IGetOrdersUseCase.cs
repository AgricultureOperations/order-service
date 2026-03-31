using Domain.Entities;

namespace Application.UseCases;

public interface IGetOrdersUseCase
{
    Task<IEnumerable<Order>?> Execute();
}
