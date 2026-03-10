using Domain.Models;
using Domain.Entities;

namespace Domain.Ports.Driving;

public interface ICreateOrderUseCase
{
    Task<Order> Execute(CreateOrderRequest Request); 
}
