using Application.UseCases;
using Domain.Ports.Driven;
using Infrastructure.Adapters.Driven;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<IOrderPersistencePort, OrderPersistenceAdapter>();
builder.Services.AddScoped<CreateOrder>();

var app = builder.Build();

app.MapControllers();
app.Run();