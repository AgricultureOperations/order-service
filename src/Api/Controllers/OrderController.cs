using Microsoft.AspNetCore.Mvc;
using Application.UseCases;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController: ControllerBase
{
    private readonly CreateOrder _createOrder;
    public OrderController(CreateOrder createOrder)
    {
        this._createOrder = createOrder;
    }
    [HttpPost]
    public async Task<IActionResult> Store(Guid customerId, decimal total)
    {
        var order = await _createOrder.Execute(customerId,total);
        return Ok(order);
    }
}
