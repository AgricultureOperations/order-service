using Api.Authorization;
using Application.DTOs;
using Application.UseCases;
using Domain.Entities;
using Domain.Ports.Driven;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Order maintainer API. v2 because the list became paginated and orders gained lines (see LegacyOrdersController).</summary>
[ApiController]
[Route("api/v2/orders")]
public class OrdersController : ControllerBase
{
    private readonly ICreateOrderUseCase _createOrderUseCase;
    private readonly IGetOrderByIdUseCase _getOrderByIdUseCase;
    private readonly IListOrdersUseCase _listOrdersUseCase;
    private readonly IUpdateOrderUseCase _updateOrderUseCase;
    private readonly IChangeOrderStatusUseCase _changeOrderStatusUseCase;
    private readonly IDeleteOrderUseCase _deleteOrderUseCase;

    public OrdersController(
        ICreateOrderUseCase createOrderUseCase,
        IGetOrderByIdUseCase getOrderByIdUseCase,
        IListOrdersUseCase listOrdersUseCase,
        IUpdateOrderUseCase updateOrderUseCase,
        IChangeOrderStatusUseCase changeOrderStatusUseCase,
        IDeleteOrderUseCase deleteOrderUseCase)
    {
        _createOrderUseCase = createOrderUseCase;
        _getOrderByIdUseCase = getOrderByIdUseCase;
        _listOrdersUseCase = listOrdersUseCase;
        _updateOrderUseCase = updateOrderUseCase;
        _changeOrderStatusUseCase = changeOrderStatusUseCase;
        _deleteOrderUseCase = deleteOrderUseCase;
    }

    /// <summary>GET /api/v2/orders?page&amp;pageSize&amp;search&amp;statusId&amp;paymentStatus&amp;from&amp;to&amp;sort&amp;order</summary>
    [Authorize(Policy = Permissions.OrdersView)]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderSummaryResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? statusId = null,
        [FromQuery] string? paymentStatus = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null)
    {
        PaymentStatus? payment = null;
        if (!string.IsNullOrEmpty(paymentStatus))
        {
            if (!Enum.TryParse<PaymentStatus>(paymentStatus, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return BadRequest(new { message = $"paymentStatus must be one of: {string.Join(", ", Enum.GetNames<PaymentStatus>())}." });
            payment = parsed;
        }

        var sortField = OrderSortField.CreatedAt;
        if (!string.IsNullOrEmpty(sort) && !Enum.TryParse(sort, ignoreCase: true, out sortField))
            return BadRequest(new { message = "sort must be one of: createdAt, orderNumber, total, customerName." });

        if (!string.IsNullOrEmpty(order) && order is not ("asc" or "desc"))
            return BadRequest(new { message = "order must be asc or desc." });

        var result = await _listOrdersUseCase.Execute(new OrderListQuery
        {
            Page = page,
            PageSize = pageSize,
            Search = search,
            StatusId = statusId,
            PaymentStatus = payment,
            From = from?.UtcDateTime,
            To = to?.UtcDateTime,
            Sort = sortField,
            Descending = order != "asc",
        });
        return Ok(result.ToResponse());
    }

    [Authorize(Policy = Permissions.OrdersView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id)
    {
        var found = await _getOrderByIdUseCase.Execute(id);
        if (found is null) return NotFound(new { message = "Order not found" });
        return Ok(found.ToResponse());
    }

    /// <summary>Creates a Pending order. Send an Idempotency-Key header so a retried request returns the same order.</summary>
    [Authorize(Policy = Permissions.OrdersCreate)]
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var created = await _createOrderUseCase.Execute(request, User.UserId(), idempotencyKey);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    /// <summary>Replaces customer, notes and lines. Only while Pending; a stale version returns 409.</summary>
    [Authorize(Policy = Permissions.OrdersEdit)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> Update(Guid id, [FromBody] UpdateOrderRequest request)
    {
        var updated = await _updateOrderUseCase.Execute(id, request, User.UserId());
        return Ok(updated.ToResponse());
    }

    [Authorize(Policy = Permissions.OrdersEdit)]
    [HttpPost("{id:guid}/confirm")]
    public Task<ActionResult<OrderResponse>> Confirm(Guid id, [FromBody] OrderTransitionRequest request) =>
        Transition(id, OrderTransition.Confirm, request.Version, null);

    [Authorize(Policy = Permissions.OrdersEdit)]
    [HttpPost("{id:guid}/ship")]
    public Task<ActionResult<OrderResponse>> Ship(Guid id, [FromBody] OrderTransitionRequest request) =>
        Transition(id, OrderTransition.Ship, request.Version, null);

    [Authorize(Policy = Permissions.OrdersEdit)]
    [HttpPost("{id:guid}/deliver")]
    public Task<ActionResult<OrderResponse>> Deliver(Guid id, [FromBody] OrderTransitionRequest request) =>
        Transition(id, OrderTransition.Deliver, request.Version, null);

    [Authorize(Policy = Permissions.OrdersEdit)]
    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<OrderResponse>> Cancel(Guid id, [FromBody] CancelOrderRequest request) =>
        Transition(id, OrderTransition.Cancel, request.Version, request.Reason);

    /// <summary>Soft delete: hides a Pending or Cancelled order without payments. The row is kept for audit.</summary>
    [Authorize(Policy = Permissions.OrdersDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _deleteOrderUseCase.Execute(id, User.UserId());
        return NoContent();
    }

    private async Task<ActionResult<OrderResponse>> Transition(Guid id, OrderTransition transition, uint version, string? reason)
    {
        var changed = await _changeOrderStatusUseCase.Execute(id, transition, version, reason, User.UserId());
        return Ok(changed.ToResponse());
    }
}
