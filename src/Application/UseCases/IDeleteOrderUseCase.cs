using Domain.Entities;

namespace Application.UseCases;

public interface IDeleteOrderUseCase
{
    Task<Order> Execute(Guid Id);
}
