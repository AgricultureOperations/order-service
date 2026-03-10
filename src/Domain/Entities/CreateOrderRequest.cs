namespace Domain.Entities;

public class CreateOrderRequest
{
    public Guid customerId { get; set; }
    public decimal total { get; set; }
}
