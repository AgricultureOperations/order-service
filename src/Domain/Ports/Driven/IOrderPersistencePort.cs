using Domain.Models;

namespace Domain.Ports.Driven;

public interface IOrderPersistencePort
{
    Task Save(Order order);
}
