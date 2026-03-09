using Domain.Models;

namespace Domain.Ports.Driving;

public interface ICreateOrderUseCase
{
    Task<Order> Execute(Guid customerId, decimal total); 
}
