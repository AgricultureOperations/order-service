namespace Domain.Entities;

public class Order
{
    public Guid Id {get; private set;}    
    public Guid CustomerId {get; private set;}    
    public decimal total {get; private set;}    
    
    public Order() {}
    
    public Order(Guid CustomerId, decimal total)
    {
        this.Id = Guid.NewGuid();
        this.CustomerId = CustomerId;
        this.total = total;
    }
    
    public void UpdateTotal(decimal total)
    {
        if (total < 0) throw new ArgumentException("Total cannot be negative");
        this.total = total;
    }

    public void UpdateCustomerId(Guid customerId)
    {
        this.CustomerId = customerId;
    }

    public void UpdateOrder(Guid customerId,decimal total)
    {
        // Combine multiple updates in one method if needed
        UpdateCustomerId(customerId);
        UpdateTotal(total);
    }
}
