using Microsoft.AspNetCore.Mvc;
using Domain.Ports.Driving;
using Domain.Entities;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController: ControllerBase
{
    private readonly ICreateOrderUseCase _createOrderUseCase;
    private readonly IGetOrderByIdUseCase _getByIdOrderUseCase;
    public OrderController(ICreateOrderUseCase createOrderUseCase,IGetOrderByIdUseCase getByIdOrderUseCase)
    {
        this._createOrderUseCase = createOrderUseCase;
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
    public async Task<IActionResult> Store([FromBody]CreateOrderRequest request)
    {
        var order = await _createOrderUseCase.Execute(request.customerId,request.total);
        return Ok(order);
    }
}
