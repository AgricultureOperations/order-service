using Api.Authorization;
using Application.DTOs;
using Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Deprecated v1 read routes, kept in their original shape (bare array, <c>{ id, customerId, total, createdAt, statusId, status }</c>)
/// for frontends deployed before /api/v2/orders. The v1 writes were removed: no client used them, and they could
/// not create a valid order (no lines). Remove these routes once every frontend calls v2.
/// </summary>
[ApiController]
[Route("api/v1/orders")]
public class LegacyOrdersController : ControllerBase
{
    private readonly IGetOrdersUseCase _getOrdersUseCase;
    private readonly IGetOrderByIdUseCase _getOrderByIdUseCase;

    public LegacyOrdersController(IGetOrdersUseCase getOrdersUseCase, IGetOrderByIdUseCase getOrderByIdUseCase)
    {
        _getOrdersUseCase = getOrdersUseCase;
        _getOrderByIdUseCase = getOrderByIdUseCase;
    }

    [Authorize(Policy = Permissions.OrdersView)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LegacyOrderResponse>>> GetAll()
    {
        var orders = await _getOrdersUseCase.Execute();
        return Ok(orders.Select(o => o.ToLegacy()));
    }

    [Authorize(Policy = Permissions.OrdersView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LegacyOrderResponse>> GetById(Guid id)
    {
        var order = await _getOrderByIdUseCase.Execute(id);
        if (order is null) return NotFound(new { message = "Order not found" });
        return Ok(order.ToLegacy());
    }
}
