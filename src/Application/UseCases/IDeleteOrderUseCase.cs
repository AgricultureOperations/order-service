using Domain.Entities;

namespace Application.UseCases;

public interface IDeleteOrderUseCase
{
    Task Execute(Guid Id);
}
