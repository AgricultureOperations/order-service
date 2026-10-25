using System.Net;
using System.Net.Http.Json;
using Api.Tests.Support;

namespace Api.Tests;

[Collection("api")]
public class OrderSettingsApiTests
{
    private readonly HttpClient _client;
    public OrderSettingsApiTests(ApiFactory factory) => _client = factory.AdminClient();

    private static long Sequence(string number) => long.Parse(number.Split('-')[2]);

    [Fact]
    public async Task The_prefix_is_seeded_as_ORD_and_the_next_number_is_reported()
    {
        var settings = await (await _client.GetAsync("/api/v1/order-settings")).Json();
        Assert.Equal("ORD", (string)settings["orderNumberPrefix"]!);
        Assert.Matches($"^ORD-{DateTime.UtcNow.Year}-\\d{{6}}$", (string)settings["nextNumber"]!);

        // Peeking doesn't reserve: the next order gets exactly the reported number.
        var created = await _client.CreateOrder();
        var settingsAfter = await (await _client.GetAsync("/api/v1/order-settings")).Json();
        Assert.Equal((string)settings["nextNumber"]!, (string)created["orderNumber"]!);
        Assert.Equal(Sequence((string)created["orderNumber"]!) + 1, Sequence((string)settingsAfter["nextNumber"]!));
    }

    [Fact]
    public async Task Each_prefix_has_its_own_yearly_counter()
    {
        var year = DateTime.UtcNow.Year;
        var before = await _client.CreateOrder();
        try
        {
            var update = await _client.PutAsJsonAsync("/api/v1/order-settings", new { orderNumberPrefix = "agr" });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var body = await update.Json();
            Assert.Equal("AGR", (string)body["orderNumberPrefix"]!);
            Assert.Equal($"AGR-{year}-000001", (string)body["nextNumber"]!);
            Assert.Equal(ApiFactory.UserId.ToString(), (string)body["updatedBy"]!);

            // A new prefix starts at 000001.
            Assert.Equal($"AGR-{year}-000001", (string)(await _client.CreateOrder())["orderNumber"]!);
            Assert.Equal($"AGR-{year}-000002", (string)(await _client.CreateOrder())["orderNumber"]!);

            // Switching back continues the ORD counter where it stopped.
            await _client.PutAsJsonAsync("/api/v1/order-settings", new { orderNumberPrefix = "ORD" });
            var back = await _client.CreateOrder();
            Assert.Equal(Sequence((string)before["orderNumber"]!) + 1, Sequence((string)back["orderNumber"]!));

            // Existing numbers never change.
            var old = await (await _client.GetAsync($"/api/v2/orders/{before["id"]}")).Json();
            Assert.Equal((string)before["orderNumber"]!, (string)old["orderNumber"]!);
        }
        finally
        {
            await _client.PutAsJsonAsync("/api/v1/order-settings", new { orderNumberPrefix = "ORD" });
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("9AB")]
    [InlineData("OR-D")]
    [InlineData("ABCDEFGHIJK")]
    public async Task Invalid_prefixes_are_400(string prefix)
    {
        var response = await _client.PutAsJsonAsync("/api/v1/order-settings", new { orderNumberPrefix = prefix });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("prefix", (string)(await response.Json())["message"]!);
    }
}
