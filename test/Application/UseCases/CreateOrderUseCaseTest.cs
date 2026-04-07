using Moq;
using Domain.Ports.Driven;
using Application.UseCases;
using Application.DTOs;
using Domain.Entities;

namespace Application.UseCases;

public class CreateOrderUseCasesTest
{
    [Fact]
    public async Task Should_Create_Order_And_Call_Repository()
    {
        //arrange
        var mockRepository = new Mock<IOrderPersistencePort>();
        var useCase = new CreateOrderUseCase(mockRepository.Object);
        var request = new CreateOrderRequest
        {
            customerId = Guid.NewGuid(),
            total = 100
        };

        //Act
        var result = await useCase.Execute(request);
        //Assert
        //1. Validate returned entity
        Assert.NotNull(result);
        Assert.Equal(request.total,result.total);
        Assert.Equal(request.customerId,result.CustomerId);

        // 2. Validate repository was called
        mockRepository.Verify(repo =>
            repo.CreateOrder(It.IsAny<Order>()),
            Times.Once
        );
    }
}