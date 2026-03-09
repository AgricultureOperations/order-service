using Domain.Models;

namespace Domain.Ports.Driven;

public interface IOrderPersistencePort
{
    Task<Order?> GetById(Guid id);
    Task Save(Order order);
}
