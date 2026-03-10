using Microsoft.EntityFrameworkCore;
using Application.UseCases;
using Domain.Ports.Driven;
using Infrastructure.Adapters.Driven;
using Infrastructure.Persistence;
using Domain.Ports.Driving;
using Application.UserCases;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IOrderPersistencePort, OrderPersistenceAdapter>();
builder.Services.AddScoped<ICreateOrderUseCase,CreateOrderUseCase>();
builder.Services.AddScoped<IGetOrderByIdUseCase,GetByIdOrderUseCase>();
builder.Services.AddScoped<IGetOrdersUseCase,GetOrdersUseCase>();
builder.Services.AddScoped<IUpdateOrderUseCase,UpdateOrderUseCase>();
builder.Services.AddScoped<IDeleteOrderUseCase,DeleteOrderUseCase>();

var app = builder.Build();

app.MapControllers();
app.Run();