using Api.Authorization;
using Application.DTOs;
using Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/order-statuses")]
public class OrderStatusesController : ControllerBase
{
    private readonly IGetOrderStatusesUseCase _getOrderStatusesUseCase;

    public OrderStatusesController(IGetOrderStatusesUseCase getOrderStatusesUseCase)
    {
        _getOrderStatusesUseCase = getOrderStatusesUseCase;
    }

    [Authorize(Policy = Permissions.OrdersView)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderStatusResponse>>> GetAll()
    {
        var statuses = await _getOrderStatusesUseCase.Execute();
        return Ok(statuses.Select(s => new OrderStatusResponse(s.Id, s.Name)));
    }
}
