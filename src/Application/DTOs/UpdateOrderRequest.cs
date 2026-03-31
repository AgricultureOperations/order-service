namespace Application.DTOs;

public class UpdateOrderRequest
{
    public Guid customerId { get; set; }
    public decimal total { get; set; }
}
