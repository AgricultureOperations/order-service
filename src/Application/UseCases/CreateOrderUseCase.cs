using Application.DTOs;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class CreateOrderUseCase : ICreateOrderUseCase
{
    public const int MaxIdempotencyKeyLength = 100;

    private readonly IOrderPersistencePort _orderPersistencePort;
    private readonly IOrderNumberGeneratorPort _orderNumberGenerator;
    private readonly IUnitOfWorkPort _unitOfWork;
    private readonly TimeProvider _clock;

    public CreateOrderUseCase(IOrderPersistencePort orderPersistencePort, IOrderNumberGeneratorPort orderNumberGenerator,
        IUnitOfWorkPort unitOfWork, TimeProvider clock)
    {
        _orderPersistencePort = orderPersistencePort;
        _orderNumberGenerator = orderNumberGenerator;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Order> Execute(CreateOrderRequest request, Guid? userId, string? idempotencyKey)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (key?.Length > MaxIdempotencyKeyLength)
            throw new DomainValidationException($"Idempotency-Key is longer than {MaxIdempotencyKeyLength} characters.");

        if (key is not null)
        {
            var existing = await _orderPersistencePort.GetByIdempotencyKey(key);
            if (existing is not null) return existing;
        }

        // Validate before taking a number, so a bad request never holds the counter lock.
        var (customerId, customer) = request.ToCustomer();
        var lines = request.ToLines();
        var now = _clock.GetUtcNow().UtcDateTime;

        try
        {
            // Number and insert share one transaction: a failed insert rolls the counter back, so there are no gaps.
            return await _unitOfWork.InTransaction(async () =>
            {
                var number = await _orderNumberGenerator.Next(now.Year);
                var order = Order.Create(number, customerId, customer, request.Currency, request.Notes, lines, userId, now);
                return await _orderPersistencePort.CreateOrder(order, key);
            });
        }
        catch (IdempotencyKeyConflictException) when (key is not null)
        {
            // A concurrent request with the same key committed first: return its order.
            return await _orderPersistencePort.GetByIdempotencyKey(key) ?? throw new IdempotencyKeyConflictException();
        }
    }
}
