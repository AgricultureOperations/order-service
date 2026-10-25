using Microsoft.EntityFrameworkCore;
using Api.Authorization;
using Api.Middleware;
using Application.UseCases;
using Domain.Ports.Driven;
using Infrastructure.Adapters.Driven;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    // Keep JWT claim names as issued ("sub", "permissions") instead of mapping them to WS-* URIs.
    options.MapInboundClaims = false;

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

    options.Events = new JwtBearerEvents
    {
        // 401 keeps WWW-Authenticate but also carries { message }, like every other error.
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
        },
    };
});

// One policy per permission code: a valid token without the code gets 403 (JsonAuthorizationResultHandler).
// Tokens issued before RBAC carry no "permissions" claim and are therefore treated as having none.
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim(Permissions.ClaimType, permission));
});
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, JsonAuthorizationResultHandler>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCorsPolicy", policy =>
        {
            policy.WithOrigins(builder.Configuration.GetConnectionString("FrontendHost")!) // your frontend
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("Location")
                  .AllowCredentials(); // if you use cookies/auth
        });
    });

    builder.WebHost.UseUrls("http://0.0.0.0:"+builder.Configuration.GetConnectionString("ServerPort")!);

    builder.Services.AddControllers();
    // Model binding errors (malformed JSON, wrong types) answer 400 { message } instead of ProblemDetails.
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var first = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .Select(e => $"{e.Key}: {e.Value!.Errors[0].ErrorMessage}")
                .FirstOrDefault() ?? "The request is not valid.";
            return new BadRequestObjectResult(new { message = first });
        };
    });

    builder.Services.AddDbContext<OrderDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddSingleton(TimeProvider.System);

    builder.Services.AddScoped<IOrderPersistencePort, OrderPersistenceAdapter>();
    builder.Services.AddScoped<IOrderNumberGeneratorPort, OrderNumberGeneratorAdapter>();
    builder.Services.AddScoped<ISettingsPersistencePort, SettingsPersistenceAdapter>();
    builder.Services.AddScoped<IUnitOfWorkPort, UnitOfWorkAdapter>();

    builder.Services.AddScoped<ICreateOrderUseCase, CreateOrderUseCase>();
    builder.Services.AddScoped<IGetOrderByIdUseCase, GetOrderByIdUseCase>();
    builder.Services.AddScoped<IListOrdersUseCase, ListOrdersUseCase>();
    builder.Services.AddScoped<IGetOrdersUseCase, GetOrdersUseCase>();
    builder.Services.AddScoped<IUpdateOrderUseCase, UpdateOrderUseCase>();
    builder.Services.AddScoped<IChangeOrderStatusUseCase, ChangeOrderStatusUseCase>();
    builder.Services.AddScoped<IDeleteOrderUseCase, DeleteOrderUseCase>();
    builder.Services.AddScoped<IGetOrderStatusesUseCase, GetOrderStatusesUseCase>();
    builder.Services.AddScoped<IGetOrderSettingsUseCase, GetOrderSettingsUseCase>();
    builder.Services.AddScoped<IUpdateOrderSettingsUseCase, UpdateOrderSettingsUseCase>();

    var app = builder.Build();
    // CORS first, so error responses written below still carry the CORS headers the browser needs to read them.
    app.UseCors("DevCorsPolicy");
    app.UseMiddleware<ErrorHandlingMiddleware>();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.Run();

// Exposed for WebApplicationFactory in test/Api.
public partial class Program { }
