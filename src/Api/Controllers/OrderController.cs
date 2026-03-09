using Microsoft.AspNetCore.Mvc;
using Application.UseCases;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController: ControllerBase
{
    private readonly CreateOrder _createOrder;
    private readonly GetByIdOrderUseCase _getByIdOrderUseCase;
    public OrderController(CreateOrder createOrder,GetByIdOrderUseCase getByIdOrderUseCase)
    {
        this._createOrder = createOrder;
        this._getByIdOrderUseCase = getByIdOrderUseCase;
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _getByIdOrderUseCase.Execute(id);
        if (order == null)
            return NotFound();
        return Ok(order);
    }
    [HttpPost]
    public async Task<IActionResult> Store(Guid customerId, decimal total)
    {
        await _createOrder.Execute(customerId,total);
        return Ok();
    }
}
