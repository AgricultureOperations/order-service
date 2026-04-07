using Microsoft.EntityFrameworkCore;
using Application.UseCases;
using Domain.Ports.Driven;
using Infrastructure.Adapters.Driven;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
    throw new Exception("Jwt:Secret is missing");
var jwtValidIssuer = builder.Configuration["Jwt:Issuer"];
if (string.IsNullOrEmpty(jwtValidIssuer))
    throw new Exception("Jwt:Issuer is missing");
var jwtVaidAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrEmpty(jwtVaidAudience))
    throw new Exception("Jwt:Audience is missing");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // true in production
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtValidIssuer,

        ValidateAudience = true,
        ValidAudience = jwtVaidAudience,
        
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSecret)
        ),

        RequireSignedTokens = true,
        RequireExpirationTime = true,
    };
});

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
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.Run();