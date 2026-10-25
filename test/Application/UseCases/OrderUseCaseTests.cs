using Application.DTOs;
using Application.Tests.Support;
using Application.UseCases;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Ports.Driven;
using Moq;

namespace Application.Tests.UseCases;

public class CreateOrderUseCaseTests
{
    private readonly Mock<IOrderPersistencePort> _orders = new();
    private readonly Mock<IOrderNumberGeneratorPort> _numbers = new();
    private readonly PassThroughUnitOfWork _unitOfWork = new();
    private CreateOrderUseCase UseCase => new(_orders.Object, _numbers.Object, _unitOfWork, TestData.Clock);

    public CreateOrderUseCaseTests()
    {
        _numbers.Setup(n => n.Next(2026)).ReturnsAsync("ORD-2026-000007");
        _orders.Setup(o => o.CreateOrder(It.IsAny<Order>(), It.IsAny<string?>()))
            .ReturnsAsync((Order order, string? _) => order);
    }

    [Fact]
    public async Task Creates_a_pending_order_numbered_for_the_current_year()
    {
        var order = await UseCase.Execute(TestData.CreateRequest(), TestData.UserId, null);

        Assert.Equal("ORD-2026-000007", order.OrderNumber);
        Assert.Equal(TestData.CustomerId, order.CustomerId);
        Assert.Equal(1, _unitOfWork.Calls);
        Assert.Equal(OrderStatusIds.Pending, order.StatusId);
        Assert.Equal("USD", order.Currency);
        Assert.Equal(TestData.UserId, order.CreatedBy);
        Assert.Equal(76.50m, order.Subtotal);
        Assert.Equal(11.48m, order.TaxTotal);  // 76.50 × 0.15 = 11.475 → 11.48
        Assert.Equal(87.98m, order.Total);
        _orders.Verify(o => o.CreateOrder(order, null), Times.Once);
    }

    [Fact]
    public async Task A_known_idempotency_key_returns_the_existing_order_without_a_new_number()
    {
        var existing = TestData.PendingOrder();
        _orders.Setup(o => o.GetByIdempotencyKey("key-1")).ReturnsAsync(existing);

        var order = await UseCase.Execute(TestData.CreateRequest(), TestData.UserId, " key-1 ");

        Assert.Same(existing, order);
        _numbers.Verify(n => n.Next(It.IsAny<int>()), Times.Never);
        _orders.Verify(o => o.CreateOrder(It.IsAny<Order>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task A_new_idempotency_key_is_stored_with_the_order()
    {
        await UseCase.Execute(TestData.CreateRequest(), TestData.UserId, "key-2");
        _orders.Verify(o => o.CreateOrder(It.IsAny<Order>(), "key-2"), Times.Once);
    }

    [Fact]
    public async Task Invalid_requests_fail_before_a_sequence_number_is_taken()
    {
        var request = TestData.CreateRequest();
        request.Lines![0].Quantity = 0;

        await Assert.ThrowsAsync<DomainValidationException>(() => UseCase.Execute(request, TestData.UserId, null));
        _numbers.Verify(n => n.Next(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task A_lost_idempotency_race_returns_the_winning_order()
    {
        var winner = TestData.PendingOrder();
        _orders.SetupSequence(o => o.GetByIdempotencyKey("key-3"))
            .ReturnsAsync((Order?)null)   // before the transaction: nobody has the key yet
            .ReturnsAsync(winner);        // after the rollback: the concurrent request committed it
        _orders.Setup(o => o.CreateOrder(It.IsAny<Order>(), "key-3")).ThrowsAsync(new IdempotencyKeyConflictException());

        var order = await UseCase.Execute(TestData.CreateRequest(), TestData.UserId, "key-3");

        Assert.Same(winner, order);
    }

    [Fact]
    public async Task A_customer_without_an_id_is_a_validation_error()
    {
        var request = TestData.CreateRequest();
        request.Customer!.Id = Guid.Empty;
        await Assert.ThrowsAsync<DomainValidationException>(() => UseCase.Execute(request, TestData.UserId, null));
        _numbers.Verify(n => n.Next(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task A_missing_customer_is_a_validation_error()
    {
        var request = TestData.CreateRequest();
        request.Customer = null;
        await Assert.ThrowsAsync<DomainValidationException>(() => UseCase.Execute(request, TestData.UserId, null));
    }

    [Fact]
    public async Task An_overlong_idempotency_key_is_rejected()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            UseCase.Execute(TestData.CreateRequest(), TestData.UserId, new string('k', 101)));
    }
}

public class UpdateOrderUseCaseTests
{
    private readonly Mock<IOrderPersistencePort> _orders = new();
    private UpdateOrderUseCase UseCase => new(_orders.Object, TestData.Clock);

    private static UpdateOrderRequest RequestFor(Order order)
    {
        var create = TestData.CreateRequest();
        return new UpdateOrderRequest { Customer = create.Customer, Lines = create.Lines, Version = order.Version, Notes = "Updated" };
    }

    [Fact]
    public async Task Updates_a_pending_order_with_the_version_the_client_read()
    {
        var order = TestData.PendingOrder();
        _orders.Setup(o => o.GetById(order.Id)).ReturnsAsync(order);

        var updated = await UseCase.Execute(order.Id, RequestFor(order), TestData.UserId);

        Assert.Equal("Updated", updated.Notes);
        Assert.Equal(87.98m, updated.Total);
        _orders.Verify(o => o.UpdateOrder(order, order.Version), Times.Once);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict()
    {
        var order = TestData.PendingOrder();
        _orders.Setup(o => o.GetById(order.Id)).ReturnsAsync(order);
        var request = RequestFor(order);
        request.Version = order.Version + 1;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => UseCase.Execute(order.Id, request, TestData.UserId));
        _orders.Verify(o => o.UpdateOrder(It.IsAny<Order>(), It.IsAny<uint>()), Times.Never);
    }

    [Fact]
    public async Task An_unknown_order_is_not_found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            UseCase.Execute(Guid.NewGuid(), new UpdateOrderRequest(), TestData.UserId));
    }
}

public class ChangeOrderStatusUseCaseTests
{
    private readonly Mock<IOrderPersistencePort> _orders = new();
    private ChangeOrderStatusUseCase UseCase => new(_orders.Object, TestData.Clock);

    [Theory]
    [InlineData(OrderStatusIds.Pending, OrderTransition.Confirm, OrderStatusIds.Confirmed)]
    [InlineData(OrderStatusIds.Confirmed, OrderTransition.Ship, OrderStatusIds.Shipped)]
    [InlineData(OrderStatusIds.Shipped, OrderTransition.Deliver, OrderStatusIds.Delivered)]
    [InlineData(OrderStatusIds.Confirmed, OrderTransition.Cancel, OrderStatusIds.Cancelled)]
    public async Task Applies_the_transition_and_saves(int from, OrderTransition transition, int expected)
    {
        var order = TestData.OrderIn(from);
        _orders.Setup(o => o.GetById(order.Id)).ReturnsAsync(order);

        var changed = await UseCase.Execute(order.Id, transition, order.Version, "Customer request", TestData.UserId);

        Assert.Equal(expected, changed.StatusId);
        _orders.Verify(o => o.UpdateOrder(order, order.Version), Times.Once);
    }

    [Fact]
    public async Task An_invalid_transition_is_a_conflict_and_nothing_is_saved()
    {
        var order = TestData.PendingOrder();
        _orders.Setup(o => o.GetById(order.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            UseCase.Execute(order.Id, OrderTransition.Deliver, order.Version, null, TestData.UserId));
        _orders.Verify(o => o.UpdateOrder(It.IsAny<Order>(), It.IsAny<uint>()), Times.Never);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict()
    {
        var order = TestData.PendingOrder();
        _orders.Setup(o => o.GetById(order.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            UseCase.Execute(order.Id, OrderTransition.Confirm, order.Version + 3, null, TestData.UserId));
    }
}

public class DeleteOrderUseCaseTests
{
    [Fact]
    public async Task Soft_deletes_and_saves_the_order()
    {
        var orders = new Mock<IOrderPersistencePort>();
        var order = TestData.PendingOrder();
        orders.Setup(o => o.GetById(order.Id)).ReturnsAsync(order);

        await new DeleteOrderUseCase(orders.Object, TestData.Clock).Execute(order.Id, TestData.UserId);

        Assert.True(order.IsDeleted);
        Assert.Equal(TestData.UserId, order.DeletedBy);
        orders.Verify(o => o.UpdateOrder(order, order.Version), Times.Once);
    }

    [Fact]
    public async Task An_unknown_order_is_not_found()
    {
        var useCase = new DeleteOrderUseCase(new Mock<IOrderPersistencePort>().Object, TestData.Clock);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.Execute(Guid.NewGuid(), TestData.UserId));
    }
}

public class ListOrdersUseCaseTests
{
    private readonly Mock<IOrderPersistencePort> _orders = new();
    private ListOrdersUseCase UseCase => new(_orders.Object);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Rejects_invalid_paging(int page, int pageSize)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            UseCase.Execute(new OrderListQuery { Page = page, PageSize = pageSize }));
    }

    [Fact]
    public async Task Rejects_an_empty_date_range()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            UseCase.Execute(new OrderListQuery { From = TestData.Now, To = TestData.Now }));
    }

    [Fact]
    public async Task Trims_the_search_and_drops_it_when_blank()
    {
        _orders.Setup(o => o.List(It.IsAny<OrderListQuery>()))
            .ReturnsAsync(new PagedResult<Order>(Array.Empty<Order>(), 1, 10, 0));

        await UseCase.Execute(new OrderListQuery { Search = "  esperanza " });
        await UseCase.Execute(new OrderListQuery { Search = "   " });

        _orders.Verify(o => o.List(It.Is<OrderListQuery>(q => q.Search == "esperanza")), Times.Once);
        _orders.Verify(o => o.List(It.Is<OrderListQuery>(q => q.Search == null)), Times.Once);
    }
}

public class OrderSettingsUseCaseTests
{
    private readonly Mock<ISettingsPersistencePort> _settings = new();
    private readonly Mock<IOrderNumberGeneratorPort> _numbers = new();

    public OrderSettingsUseCaseTests()
    {
        _numbers.Setup(n => n.Peek(It.IsAny<string>(), 2026)).ReturnsAsync((string prefix, int year) => $"{prefix}-{year}-000001");
    }

    [Fact]
    public async Task Updating_the_prefix_normalizes_it_and_records_the_user()
    {
        var setting = new Setting(SettingKeys.OrderNumberPrefix, "ORD");
        _settings.Setup(s => s.Get(SettingKeys.OrderNumberPrefix)).ReturnsAsync(setting);

        var result = await new UpdateOrderSettingsUseCase(_settings.Object, _numbers.Object, TestData.Clock)
            .Execute(new UpdateOrderSettingsRequest { OrderNumberPrefix = " agr " }, TestData.UserId);

        Assert.Equal("AGR", result.OrderNumberPrefix);
        Assert.Equal("AGR-2026-000001", result.NextNumber);
        Assert.Equal(TestData.UserId, setting.UpdatedBy);
        _settings.Verify(s => s.Save(setting), Times.Once);
    }

    [Fact]
    public async Task An_invalid_prefix_is_rejected_without_saving()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            new UpdateOrderSettingsUseCase(_settings.Object, _numbers.Object, TestData.Clock)
                .Execute(new UpdateOrderSettingsRequest { OrderNumberPrefix = "1-bad" }, TestData.UserId));
        _settings.Verify(s => s.Save(It.IsAny<Setting>()), Times.Never);
    }

    [Fact]
    public async Task Reading_the_settings_returns_the_prefix_and_the_next_number()
    {
        _settings.Setup(s => s.Get(SettingKeys.OrderNumberPrefix)).ReturnsAsync(new Setting(SettingKeys.OrderNumberPrefix, "ORD"));
        _numbers.Setup(n => n.Peek("ORD", 2026)).ReturnsAsync("ORD-2026-000043");
        var result = await new GetOrderSettingsUseCase(_settings.Object, _numbers.Object, TestData.Clock).Execute();
        Assert.Equal("ORD", result.OrderNumberPrefix);
        Assert.Equal("ORD-2026-000043", result.NextNumber);
    }
}
