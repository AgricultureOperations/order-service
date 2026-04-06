    using Microsoft.EntityFrameworkCore;
    using Application.UseCases;
    using Domain.Ports.Driven;
    using Infrastructure.Adapters.Driven;
    using Infrastructure.Persistence;

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCorsPolicy", policy =>
        {
            policy.WithOrigins("https://agricultureops.netlify.app") // your frontend
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // if you use cookies/auth
        });
    });

    builder.WebHost.UseUrls("http://0.0.0.0:8080");

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
    app.UseCors("DevCorsPolicy");

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }

    app.MapControllers();
    app.Run();