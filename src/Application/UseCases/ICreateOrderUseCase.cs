using Application.DTOs;
using Domain.Entities;

namespace Application.UseCases;

public interface ICreateOrderUseCase
{
    Task<Order> Execute(CreateOrderRequest request); 
}
