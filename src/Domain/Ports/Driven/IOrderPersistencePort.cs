using Domain.Entities;

namespace Domain.Ports.Driven;

public interface IOrderPersistencePort
{
    Task CreateOrder(Order order);
    Task <Order?> GetById(Guid id);
    Task <IEnumerable<Order>?> GetAll();
    Task UpdateOrder(Order order);
    Task DeleteOrder(Order order);
}
