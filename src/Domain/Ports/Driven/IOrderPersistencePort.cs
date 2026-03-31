using Domain.Entities;

namespace Domain.Ports.Driven;

public interface IOrderPersistencePort
{
    Task CreateOrder(Order order);
    Task <Order?> GetById(Guid id);
    Task <IEnumerable<Order>?> GetAll();
    Task UpdateOrder(Guid Id,Order order);
    Task <Order> DeleteOrder(Guid Id);
}
