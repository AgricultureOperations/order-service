using Application.UseCases;
using Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController: ControllerBase
{
    private readonly ICreateOrderUseCase _createOrderUseCase;
    private readonly IGetOrderByIdUseCase _getByIdOrderUseCase;
    private readonly IGetOrdersUseCase _getOrdersUseCase;
    private readonly IUpdateOrderUseCase _updateOrderUseCase;
    private readonly IDeleteOrderUseCase _deleteOrderUseCase;
    public OrderController(
        ICreateOrderUseCase createOrderUseCase
        ,IGetOrderByIdUseCase getByIdOrderUseCase
        ,IGetOrdersUseCase getOrdersUseCase
        ,IUpdateOrderUseCase updateOrderUseCase
        ,IDeleteOrderUseCase deleteOrderUseCase
        )
    {
        this._createOrderUseCase = createOrderUseCase;
        this._getByIdOrderUseCase = getByIdOrderUseCase;
        this._getOrdersUseCase = getOrdersUseCase;
        this._updateOrderUseCase = updateOrderUseCase;
        this._deleteOrderUseCase = deleteOrderUseCase;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody]CreateOrderRequest Request)
    {
        var order = await _createOrderUseCase.Execute(Request);
        return Ok(order);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _getByIdOrderUseCase.Execute(id);
        if (order == null)
            return NotFound();
        return Ok(order);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _getOrdersUseCase.Execute();
        return Ok(orders);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid Id,[FromBody] UpdateOrderRequest Request)
    {
        var order = await _updateOrderUseCase.Execute(Id,Request);
        if ( order == null )
            return NotFound();
        return Ok(order);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid Id)
    {
        var order = await _deleteOrderUseCase.Execute(Id);
        if ( order == null )
            return NotFound();
        return Ok(order);
    }
}
