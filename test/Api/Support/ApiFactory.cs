using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Api.Tests.Support;

/// <summary>
/// Boots the real API (migrations included) against a throwaway PostgreSQL container. Shared by every test class
/// in the "api" collection, so the classes run one after another on the same database.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Secret = "integration-tests-secret-at-least-32-bytes-long!";
    public const string Issuer = "auth-service";
    public const string Audience = "orders-api";
    public static readonly Guid UserId = Guid.Parse("7d4f2c1e-0000-4000-8000-000000000001");

    public static readonly string[] AllPermissions = { "orders:view", "orders:create", "orders:edit", "orders:delete", "settings:view", "settings:edit" };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:15-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Program.cs reads configuration before WebApplicationFactory can inject settings, so use env vars.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__ServerPort", "5099");
        Environment.SetEnvironmentVariable("ConnectionStrings__FrontendHost", "http://localhost:5173");
        Environment.SetEnvironmentVariable("Jwt__Secret", Secret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", Audience);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

    public HttpClient ClientWith(params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(permissions));
        return client;
    }

    public HttpClient AdminClient() => ClientWith(AllPermissions);

    public static string Token(string[]? permissions, DateTime? expires = null, string issuer = Issuer)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = UserId.ToString(),
            ["id"] = UserId.ToString(),
            ["email"] = "admin@agriops.test",
            ["role"] = "admin",
        };
        if (permissions is not null) claims["permissions"] = permissions;

        var now = DateTime.UtcNow;
        var exp = expires ?? now.AddMinutes(15);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = Audience,
            Claims = claims,
            IssuedAt = exp.AddMinutes(-30),
            NotBefore = exp.AddMinutes(-30),
            Expires = exp,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256),
        });
    }

    public async Task WithDb(Func<OrderDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<OrderDbContext>());
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }
