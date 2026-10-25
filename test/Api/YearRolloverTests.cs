using System.Net.Http.Headers;
using Api.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>Numbers restart at 000001 when the year changes; the previous year's counter is untouched.</summary>
[Collection("api")]
public class YearRolloverTests
{
    private readonly ApiFactory _factory;
    public YearRolloverTests(ApiFactory factory) => _factory = factory;

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private HttpClient ClientAt(DateTimeOffset now)
    {
        var client = _factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedClock(now))))
            .CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Token(ApiFactory.AllPermissions));
        return client;
    }

    [Fact]
    public async Task The_counter_restarts_every_year()
    {
        var dec31 = ClientAt(new DateTimeOffset(2031, 12, 31, 23, 59, 0, TimeSpan.Zero));
        Assert.Equal("ORD-2031-000001", (string)(await dec31.CreateOrder())["orderNumber"]!);
        Assert.Equal("ORD-2031-000002", (string)(await dec31.CreateOrder())["orderNumber"]!);

        var jan1 = ClientAt(new DateTimeOffset(2032, 1, 1, 0, 0, 1, TimeSpan.Zero));
        Assert.Equal("ORD-2032-000001", (string)(await jan1.CreateOrder())["orderNumber"]!);

        // A late 2031 order (clock still in 2031) continues 2031's counter.
        Assert.Equal("ORD-2031-000003", (string)(await dec31.CreateOrder())["orderNumber"]!);
    }
}
