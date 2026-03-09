using Microsoft.EntityFrameworkCore;
using Application.UseCases;
using Domain.Ports.Driven;
using Infrastructure.Adapters.Driven;
using Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IOrderPersistencePort, OrderPersistenceAdapter>();
builder.Services.AddScoped<CreateOrder>();
builder.Services.AddScoped<GetByIdOrderUseCase>();

var app = builder.Build();

app.MapControllers();
app.Run();