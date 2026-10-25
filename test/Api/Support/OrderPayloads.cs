using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Tests.Support;

public static class OrderPayloads
{
    /// <summary>The auth-service customer every payload refers to (orders store its id plus a snapshot).</summary>
    public static readonly Guid CustomerId = Guid.Parse("5f1c2b3a-0000-4000-8000-0000000000c1");

    public static object Create(string customer = "Hacienda La Esperanza", string? taxId = "0991234567001", decimal quantity = 3, decimal unitPrice = 25.50m) => new
    {
        customer = new { id = CustomerId, name = customer, taxId, email = "compras@esperanza.ec", phone = "0991234567" },
        currency = "USD",
        notes = "Entrega en bodega",
        lines = new object[]
        {
            new { productId = Guid.NewGuid(), sku = "FERT-001", productName = "Urea 46%", unit = "bag", quantity, unitPrice, discountAmount = 0m, taxRate = 0.15m },
            new { productId = Guid.NewGuid(), sku = "SEED-010", productName = "Semilla maíz", unit = "kg", quantity = 2.5m, unitPrice = 4m, discountAmount = 1m, taxRate = 0m },
        },
    };

    public static async Task<JsonNode> Json(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text) ?? throw new InvalidOperationException("Empty body");
    }

    public static async Task<JsonNode> CreateOrder(this HttpClient client, object? payload = null, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/orders") { Content = JsonContent.Create(payload ?? Create()) };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        var response = await client.SendAsync(request);
        if ((int)response.StatusCode != 201)
            throw new InvalidOperationException($"Create failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.Json();
    }

    public static Task<HttpResponseMessage> Transition(this HttpClient client, JsonNode order, string action, string? reason = null) =>
        client.PostAsJsonAsync<object>($"/api/v2/orders/{order["id"]}/{action}",
            reason is null ? new { version = (uint)order["version"]! } : new { version = (uint)order["version"]!, reason });

    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
