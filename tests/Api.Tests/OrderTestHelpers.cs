using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Api.Tests;

public sealed record TestProduct(int ProductId, int Sku500, int Sku1000);

public static class OrderTestHelpers
{
    public const decimal Price500 = 10m;
    public const decimal Price1000 = 18m;

    public static DateTimeOffset Today => new(DateTime.UtcNow.Date, TimeSpan.Zero);

    /// <summary>Rows cannot be deleted and the database is shared, so every test orders its own product.</summary>
    public static async Task<TestProduct> NewProductAsync(this MsSqlFixture fixture, int stockG = 20_000, string currency = "EUR")
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        var product = new Product(0, "Order test", "Test origin", "Dark");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var sku500 = new Sku(0, product.Id, Grind.WholeBean, 500);
        var sku1000 = new Sku(0, product.Id, Grind.WholeBean, 1000);
        db.Skus.AddRange(sku500, sku1000);
        db.Inventories.Add(new Inventory(product.Id, stockG));
        db.InventoryMovements.Add(InventoryMovement.Restock(product.Id, stockG, Today.AddDays(-30)));
        await db.SaveChangesAsync();

        db.EffectivePrices.AddRange(
            new EffectivePrice(0, sku500.Id, Price500, currency, Today.AddDays(-30)),
            new EffectivePrice(0, sku1000.Id, Price1000, currency, Today.AddDays(-30)));
        await db.SaveChangesAsync();

        return new TestProduct(product.Id, sku500.Id, sku1000.Id);
    }

    public static object Body(params (int SkuId, int Qty)[] items) =>
        new { items = items.Select(i => new { skuId = i.SkuId, qty = i.Qty }) };

    public static Task<HttpResponseMessage> PlaceAsync(HttpClient client, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/orders") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    public static async Task<OrderResponse> PlaceOkAsync(HttpClient client, object body, string? idempotencyKey = null)
    {
        var response = await PlaceAsync(client, body, idempotencyKey);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }
}
