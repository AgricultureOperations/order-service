using Api.Authorization;
using Application.DTOs;
using Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Order configuration edited from the frontend's Maintainers hub (today: the order number prefix).</summary>
[ApiController]
[Route("api/v1/order-settings")]
public class OrderSettingsController : ControllerBase
{
    private readonly IGetOrderSettingsUseCase _getOrderSettingsUseCase;
    private readonly IUpdateOrderSettingsUseCase _updateOrderSettingsUseCase;

    public OrderSettingsController(IGetOrderSettingsUseCase getOrderSettingsUseCase, IUpdateOrderSettingsUseCase updateOrderSettingsUseCase)
    {
        _getOrderSettingsUseCase = getOrderSettingsUseCase;
        _updateOrderSettingsUseCase = updateOrderSettingsUseCase;
    }

    [Authorize(Policy = Permissions.SettingsView)]
    [HttpGet]
    public async Task<ActionResult<OrderSettingsResponse>> Get() => Ok(await _getOrderSettingsUseCase.Execute());

    [Authorize(Policy = Permissions.SettingsEdit)]
    [HttpPut]
    public async Task<ActionResult<OrderSettingsResponse>> Update([FromBody] UpdateOrderSettingsRequest request) =>
        Ok(await _updateOrderSettingsUseCase.Execute(request, User.UserId()));
}
