using Domain.Entities;

namespace Application.UseCases;

public interface IGetOrderByIdUseCase
{
    Task<Order?> Execute(Guid id);
}
