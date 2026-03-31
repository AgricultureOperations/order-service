using Application.DTOs;
using Domain.Entities;

namespace Application.UseCases;

public interface IUpdateOrderUseCase
{
    Task<Order> Execute(Guid Id, UpdateOrderRequest request);
}
