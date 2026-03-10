namespace Domain.Entities;

public class UpdateOrderRequest
{
    public Guid customerId { get; set; }
    public decimal total { get; set; }
}
