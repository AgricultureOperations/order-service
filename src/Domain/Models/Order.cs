namespace Domain.Models;

public class Order
{
    public Guid Id {get; private set;}    
    public Guid CustomerId {get; private set;}    
    public decimal total {get; private set;}    
    
    public Order(Guid CustomerId, decimal total)
    {
        this.Id = Guid.NewGuid();
        this.CustomerId = CustomerId;
        this.total = total;
    }
}
