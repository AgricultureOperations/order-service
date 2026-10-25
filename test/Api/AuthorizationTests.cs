using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Tests.Support;

namespace Api.Tests;

[Collection("api")]
public class AuthorizationTests
{
    private readonly ApiFactory _factory;
    public AuthorizationTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/v2/orders")]
    [InlineData("/api/v1/orders")]
    [InlineData("/api/v1/order-settings")]
    [InlineData("/api/v1/order-statuses")]
    public async Task No_token_is_401_with_a_message(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal("Unauthorized", (string?)(await response.Json())["message"]);
    }

    [Fact]
    public async Task An_expired_token_is_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            ApiFactory.Token(ApiFactory.AllPermissions, expires: DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v2/orders")).StatusCode);
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            ApiFactory.Token(ApiFactory.AllPermissions, issuer: "someone-else"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v2/orders")).StatusCode);
    }

    [Fact]
    public async Task A_token_without_a_permissions_claim_is_403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Token(null));
        var response = await client.GetAsync("/api/v2/orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("permission", (string?)(await response.Json())["message"]);
    }

    [Fact]
    public async Task A_viewer_can_read_but_not_write()
    {
        var viewer = _factory.ClientWith("orders:view");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v2/orders")).StatusCode);
        // Order settings need settings:*, which auth-service grants to admin only.
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/order-settings")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/v2/orders", OrderPayloads.Create())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync("/api/v1/order-settings", new { orderNumberPrefix = "XYZ" })).StatusCode);

        var order = await _factory.AdminClient().CreateOrder();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Transition(order, "confirm")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.DeleteAsync($"/api/v2/orders/{order["id"]}")).StatusCode);
    }

    [Fact]
    public async Task Order_settings_need_settings_permissions()
    {
        var reader = _factory.ClientWith("settings:view");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/v1/order-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync("/api/v1/order-settings", new { orderNumberPrefix = "XYZ" })).StatusCode);

        var editor = _factory.ClientWith("orders:view", "orders:create", "orders:edit", "orders:delete");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PutAsJsonAsync("/api/v1/order-settings", new { orderNumberPrefix = "XYZ" })).StatusCode);
    }

    [Fact]
    public async Task Each_action_needs_its_own_permission()
    {
        var creator = _factory.ClientWith("orders:create");
        var created = await creator.CreateOrder();
        // orders:create alone can't read, edit or delete.
        Assert.Equal(HttpStatusCode.Forbidden, (await creator.GetAsync($"/api/v2/orders/{created["id"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await creator.Transition(created, "confirm")).StatusCode);

        var editor = _factory.ClientWith("orders:edit");
        Assert.Equal(HttpStatusCode.OK, (await editor.Transition(created, "confirm")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.DeleteAsync($"/api/v2/orders/{created["id"]}")).StatusCode);
    }
}
