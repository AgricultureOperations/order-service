using Application.UseCases;
using Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
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

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody]CreateOrderRequest Request)
    {
        var order = await _createOrderUseCase.Execute(Request);
        return Ok(order);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _getByIdOrderUseCase.Execute(id);
        if (order == null)
            return NotFound();
        return Ok(order);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _getOrdersUseCase.Execute();
        return Ok(orders);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid Id,[FromBody] UpdateOrderRequest Request)
    {
        await _updateOrderUseCase.Execute(Id,Request);
        return Ok();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid Id)
    {
        await _deleteOrderUseCase.Execute(Id);
        return Ok();
    }
}
