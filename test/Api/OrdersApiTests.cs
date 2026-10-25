using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

[Collection("api")]
public class OrdersApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public OrdersApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.AdminClient();
    }

    [Fact]
    public async Task Create_returns_201_with_location_number_and_server_side_totals()
    {
        var response = await _client.PostAsJsonAsync("/api/v2/orders", OrderPayloads.Create());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Json();

        Assert.EndsWith($"/api/v2/orders/{order["id"]}", response.Headers.Location!.ToString());
        Assert.Matches(new Regex(@"^ORD-\d{4}-\d{6}$"), (string)order["orderNumber"]!);
        Assert.Equal(1, (int)order["statusId"]!);
        Assert.Equal("Pending", (string)order["status"]!["name"]!);
        Assert.Equal("Unpaid", (string)order["paymentStatus"]!);
        Assert.Equal(2, order["lines"]!.AsArray().Count);
        // 3 × 25.50 = 76.50 (+15% = 11.475 → 11.48) and 2.5 × 4 = 10.00 − 1.00 discount.
        Assert.Equal(86.50m, (decimal)order["subtotal"]!);
        Assert.Equal(1.00m, (decimal)order["discountTotal"]!);
        Assert.Equal(11.48m, (decimal)order["taxTotal"]!);
        Assert.Equal(96.98m, (decimal)order["total"]!);
        Assert.Equal(96.98m, (decimal)order["balanceDue"]!);
        Assert.Equal(ApiFactory.UserId.ToString(), (string)order["createdBy"]!);
        Assert.Equal("Hacienda La Esperanza", (string)order["customer"]!["name"]!);
        Assert.Equal(OrderPayloads.CustomerId.ToString(), (string)order["customer"]!["id"]!);
    }

    [Fact]
    public async Task A_client_total_is_ignored()
    {
        var payload = JsonNode.Parse(JsonContent.Create(OrderPayloads.Create()).ReadAsStringAsync().Result)!;
        payload["total"] = 1;
        var response = await _client.PostAsync("/api/v2/orders", new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(96.98m, (decimal)(await response.Json())["total"]!);
    }

    [Fact]
    public async Task Concurrent_creates_get_distinct_contiguous_numbers()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => _factory.AdminClient().CreateOrder()));
        var numbers = results.Select(o => (string)o["orderNumber"]!).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        // The counter row is locked until each insert commits, so 12 parallel creates take 12 consecutive numbers.
        var sequences = numbers.Select(n => long.Parse(n.Split('-')[2])).OrderBy(n => n).ToList();
        Assert.Equal(Enumerable.Range((int)sequences[0], 12).Select(i => (long)i), sequences);
    }

    [Fact]
    public async Task The_same_idempotency_key_returns_the_same_order_once()
    {
        var key = Guid.NewGuid().ToString();
        var first = await _client.CreateOrder(idempotencyKey: key);
        var second = await _client.CreateOrder(idempotencyKey: key);
        Assert.Equal((string)first["id"]!, (string)second["id"]!);
        Assert.Equal((string)first["orderNumber"]!, (string)second["orderNumber"]!);

        await _factory.WithDb(async db =>
            Assert.Equal(1, await db.IdempotencyKeys.CountAsync(k => k.Key == key)));
    }

    [Fact]
    public async Task Parallel_requests_with_one_idempotency_key_create_one_order()
    {
        var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _factory.AdminClient().CreateOrder(idempotencyKey: key)));
        Assert.Single(results.Select(r => (string)r["id"]!).Distinct());

        // The losing requests rolled their counter increment back: the next order takes the very next number.
        var next = await _client.CreateOrder();
        Assert.Equal(Sequence(results[0]) + 1, Sequence(next));
    }

    [Fact]
    public async Task A_failed_create_does_not_consume_a_number()
    {
        var before = await _client.CreateOrder();
        var bad = await _client.PostAsJsonAsync("/api/v2/orders", new
        {
            customer = new { id = OrderPayloads.CustomerId, name = "Finca" },
            lines = new[] { new { productId = Guid.NewGuid(), sku = "A", productName = "B", unit = "kg", quantity = 1m, unitPrice = 1.001m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var after = await _client.CreateOrder();
        Assert.Equal(Sequence(before) + 1, Sequence(after));
    }

    private static long Sequence(JsonNode order) => long.Parse(((string)order["orderNumber"]!).Split('-')[2]);

    [Theory]
    [InlineData("{\"customer\":{\"id\":\"5f1c2b3a-0000-4000-8000-0000000000c1\",\"name\":\"Finca\"},\"lines\":[]}", "at least one line")]
    [InlineData("{\"customer\":{\"name\":\"Finca\"},\"lines\":[{\"productId\":\"6a1f3f8e-4b8a-4c55-9e57-7a52c2a1b001\",\"sku\":\"A\",\"productName\":\"B\",\"unit\":\"kg\",\"quantity\":1,\"unitPrice\":1}]}", "Select the customer")]
    [InlineData("{\"lines\":[{\"productId\":\"6a1f3f8e-4b8a-4c55-9e57-7a52c2a1b001\",\"sku\":\"A\",\"productName\":\"B\",\"unit\":\"kg\",\"quantity\":1,\"unitPrice\":1}]}", "Customer is required")]
    [InlineData("{\"customer\":{\"id\":\"5f1c2b3a-0000-4000-8000-0000000000c1\",\"name\":\"Finca\"},\"lines\":[{\"productId\":\"6a1f3f8e-4b8a-4c55-9e57-7a52c2a1b001\",\"sku\":\"A\",\"productName\":\"B\",\"unit\":\"kg\",\"quantity\":0,\"unitPrice\":1}]}", "greater than 0")]
    [InlineData("{not json", "")]
    public async Task Invalid_bodies_are_400_with_a_message(string body, string expected)
    {
        var response = await _client.PostAsync("/api/v2/orders", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = (string?)(await response.Json())["message"];
        Assert.False(string.IsNullOrEmpty(message));
        Assert.Contains(expected, message);
    }

    [Fact]
    public async Task Lifecycle_edit_confirm_ship_deliver()
    {
        var order = await _client.CreateOrder();
        var id = (string)order["id"]!;

        // Edit while Pending: replaces the lines and recalculates.
        var edit = await _client.PutAsJsonAsync($"/api/v2/orders/{id}", new
        {
            customer = new { id = OrderPayloads.CustomerId, name = "Finca El Sol" },
            lines = new[] { new { productId = Guid.NewGuid(), sku = "X-1", productName = "Abono", unit = "bag", quantity = 2m, unitPrice = 10m, discountAmount = 0m, taxRate = 0m } },
            version = (uint)order["version"]!,
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Json();
        Assert.Equal(20m, (decimal)edited["total"]!);
        Assert.Single(edited["lines"]!.AsArray());
        Assert.Equal("Finca El Sol", (string)edited["customer"]!["name"]!);
        Assert.NotEqual((uint)order["version"]!, (uint)edited["version"]!);

        // The version the client read before the edit is now stale.
        var stale = await _client.Transition(order, "confirm");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("changed by someone else", (string)(await stale.Json())["message"]!);

        var confirmed = await (await _client.Transition(edited, "confirm")).Json();
        Assert.Equal(6, (int)confirmed["statusId"]!);
        Assert.NotNull(confirmed["confirmedAt"]);

        var lockedEdit = await _client.PutAsJsonAsync($"/api/v2/orders/{id}", new
        {
            customer = new { id = OrderPayloads.CustomerId, name = "Finca El Sol" },
            lines = new[] { new { productId = Guid.NewGuid(), sku = "X-1", productName = "Abono", unit = "bag", quantity = 9m, unitPrice = 10m } },
            version = (uint)confirmed["version"]!,
        });
        Assert.Equal(HttpStatusCode.Conflict, lockedEdit.StatusCode);

        var shipped = await (await _client.Transition(confirmed, "ship")).Json();
        Assert.Equal(3, (int)shipped["statusId"]!);

        var cancelShipped = await _client.Transition(shipped, "cancel", "Too late");
        Assert.Equal(HttpStatusCode.Conflict, cancelShipped.StatusCode);

        var delivered = await (await _client.Transition(shipped, "deliver")).Json();
        Assert.Equal(4, (int)delivered["statusId"]!);
        Assert.Equal("Delivered", (string)delivered["status"]!["name"]!);

        var fetched = await (await _client.GetAsync($"/api/v2/orders/{id}")).Json();
        Assert.Equal(4, (int)fetched["statusId"]!);
        Assert.Single(fetched["lines"]!.AsArray());
    }

    [Fact]
    public async Task Cancel_requires_a_reason_and_records_it()
    {
        var order = await _client.CreateOrder();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.Transition(order, "cancel", "")).StatusCode);

        var cancelled = await (await _client.Transition(order, "cancel", "Cliente desistió")).Json();
        Assert.Equal(5, (int)cancelled["statusId"]!);
        Assert.Equal("Cliente desistió", (string)cancelled["cancelReason"]!);
    }

    [Fact]
    public async Task Soft_delete_hides_the_order_but_keeps_the_row()
    {
        var order = await _client.CreateOrder(OrderPayloads.Create(customer: "Borrar Prueba"));
        var id = Guid.Parse((string)order["id"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v2/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v2/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v2/orders/{id}")).StatusCode);

        var list = await (await _client.GetAsync("/api/v2/orders?search=Borrar%20Prueba")).Json();
        Assert.Equal(0, (int)list["meta"]!["total"]!);

        await _factory.WithDb(async db =>
        {
            var row = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == id);
            Assert.True(row.IsDeleted);
            Assert.NotNull(row.DeletedAt);
            Assert.Equal(ApiFactory.UserId, row.DeletedBy);
            Assert.Equal(2, row.Lines.Count);
        });
    }

    [Fact]
    public async Task Confirmed_orders_cannot_be_deleted()
    {
        var order = await _client.CreateOrder();
        var confirmed = await (await _client.Transition(order, "confirm")).Json();
        var response = await _client.DeleteAsync($"/api/v2/orders/{confirmed["id"]}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("cancel it first", (string)(await response.Json())["message"]!);
    }

    [Fact]
    public async Task Unknown_orders_are_404_with_a_message()
    {
        var response = await _client.GetAsync($"/api/v2/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Order not found", (string)(await response.Json())["message"]!);
    }

    [Fact]
    public async Task List_is_paginated_filtered_and_searchable()
    {
        var tag = $"Lista{Guid.NewGuid():N}"[..14];
        for (var i = 0; i < 3; i++) await _client.CreateOrder(OrderPayloads.Create(customer: $"{tag} {i}", taxId: $"TAX{tag}"[..12]));
        var toConfirm = await _client.CreateOrder(OrderPayloads.Create(customer: $"{tag} confirmed"));
        await _client.Transition(toConfirm, "confirm");

        var page1 = await (await _client.GetAsync($"/api/v2/orders?search={tag}&pageSize=2&page=1")).Json();
        Assert.Equal(2, page1["data"]!.AsArray().Count);
        Assert.Equal(4, (int)page1["meta"]!["total"]!);
        Assert.Equal(2, (int)page1["meta"]!["totalPages"]!);
        Assert.Null(page1["data"]![0]!["lines"]);
        Assert.Equal(2, (int)page1["data"]![0]!["itemCount"]!);
        // Newest first by default.
        Assert.Equal($"{tag} confirmed", (string)page1["data"]![0]!["customer"]!["name"]!);

        var confirmed = await (await _client.GetAsync($"/api/v2/orders?search={tag}&statusId=6")).Json();
        Assert.Equal(1, (int)confirmed["meta"]!["total"]!);

        var byNumber = await (await _client.GetAsync($"/api/v2/orders?search={toConfirm["orderNumber"]}")).Json();
        Assert.Equal(1, (int)byNumber["meta"]!["total"]!);

        var asc = await (await _client.GetAsync($"/api/v2/orders?search={tag}&sort=customerName&order=asc")).Json();
        Assert.Equal($"{tag} 0", (string)asc["data"]![0]!["customer"]!["name"]!);

        var unpaid = await (await _client.GetAsync($"/api/v2/orders?search={tag}&paymentStatus=unpaid")).Json();
        Assert.Equal(4, (int)unpaid["meta"]!["total"]!);

        var future = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var none = await (await _client.GetAsync($"/api/v2/orders?search={tag}&from={future}")).Json();
        Assert.Equal(0, (int)none["meta"]!["total"]!);
    }

    [Theory]
    [InlineData("pageSize=500")]
    [InlineData("page=0")]
    [InlineData("paymentStatus=maybe")]
    [InlineData("sort=color")]
    [InlineData("order=sideways")]
    public async Task Invalid_list_queries_are_400(string query)
    {
        var response = await _client.GetAsync($"/api/v2/orders?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull((string?)(await response.Json())["message"]);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var all = await (await _client.GetAsync("/api/v2/orders?search=%25")).Json();
        Assert.Equal(0, (int)all["meta"]!["total"]!);
    }

    [Fact]
    public async Task Legacy_v1_list_keeps_its_original_shape()
    {
        var created = await _client.CreateOrder();
        var list = (await (await _client.GetAsync("/api/v1/orders")).Json()).AsArray();
        var row = list.Single(o => (string)o!["id"]! == (string)created["id"]!)!;

        Assert.Equal((decimal)created["total"]!, (decimal)row["total"]!);
        Assert.Equal(1, (int)row["statusId"]!);
        Assert.Equal("Pending", (string)row["status"]!["name"]!);
        Assert.NotNull(row["createdAt"]);
        Assert.True(row.AsObject().ContainsKey("customerId"));
        Assert.Null(row["lines"]);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _client.PostAsJsonAsync("/api/v1/orders", new { customerId = Guid.NewGuid(), total = 1 })).StatusCode);
    }

    [Fact]
    public async Task Statuses_lookup_includes_confirmed()
    {
        var statuses = (await (await _client.GetAsync("/api/v1/order-statuses")).Json()).AsArray();
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, statuses.Select(s => (int)s!["id"]!));
        Assert.Equal("Confirmed", (string)statuses[5]!["name"]!);
    }
}
