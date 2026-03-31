namespace Application.DTOs;

public class CreateOrderRequest
{
    public Guid customerId { get; set; }
    public decimal total { get; set; }
}
