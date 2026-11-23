using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CoffeeShop.Api.Tests.OrderTestHelpers;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public class AdminInventoryTests(MsSqlFixture fixture)
{
    private async Task<int> StockOfAsync(int productId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Inventories
            .Where(i => i.ProductId == productId).Select(i => i.StockG).SingleAsync();
    }

    private async Task<int> NewProductWithoutInventoryAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var product = new Product(0, "No stock yet", "Test origin", "Light");
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static Task<HttpResponseMessage> RestockAsync(HttpClient client, int productId, int? grams) =>
        client.PostAsJsonAsync($"/admin/inventory/{productId}/restock", new RestockRequest(grams));

    /// <summary>Stock must always equal the sum of its movements; a product with no row counts as 0 g.</summary>
    private async Task AssertEveryProductMatchesItsLedgerAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var stock = await db.Inventories.ToDictionaryAsync(i => i.ProductId, i => i.StockG);
        var ledger = await db.InventoryMovements.GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Sum = g.Sum(m => m.DeltaG) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Sum);
        var productIds = await db.Products.Select(p => p.Id).ToListAsync();

        Assert.All(productIds, id => Assert.Equal(stock.GetValueOrDefault(id), ledger.GetValueOrDefault(id)));
    }

    [Fact]
    public async Task Inventory_lists_every_product_with_stock_and_last_movement()
    {
        var stocked = await fixture.NewProductAsync(stockG: 7_000);
        var unstocked = await NewProductWithoutInventoryAsync();
        var admin = await fixture.AdminClientAsync();

        var inventory = await admin.GetFromJsonAsync<InventoryResponse[]>("/admin/inventory");

        var item = Assert.Single(inventory!, i => i.ProductId == stocked.ProductId);
        Assert.Equal(("Order test", 7_000), (item.Name, item.StockG));
        Assert.Equal(Today.AddDays(-30), item.LastMovementAt);
        var empty = Assert.Single(inventory!, i => i.ProductId == unstocked);
        Assert.Equal((0, (DateTimeOffset?)null), (empty.StockG, empty.LastMovementAt));
    }

    [Fact]
    public async Task Restock_adds_grams_and_records_a_movement()
    {
        var product = await fixture.NewProductAsync(stockG: 1_000);
        var admin = await fixture.AdminClientAsync();

        var response = await RestockAsync(admin, product.ProductId, 2_500);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/admin/inventory/{product.ProductId}/movements", response.Headers.Location?.ToString());
        var restock = (await response.Content.ReadFromJsonAsync<RestockResponse>())!;
        Assert.Equal(3_500, restock.StockG);
        Assert.Equal(("Restock", 2_500, (int?)null), (restock.Movement.Reason, restock.Movement.DeltaG, restock.Movement.OrderId));
        Assert.Equal(3_500, await StockOfAsync(product.ProductId));
    }

    [Fact]
    public async Task First_restock_creates_the_inventory_row()
    {
        var productId = await NewProductWithoutInventoryAsync();
        var admin = await fixture.AdminClientAsync();

        Assert.Equal(HttpStatusCode.Created, (await RestockAsync(admin, productId, 800)).StatusCode);

        Assert.Equal(800, await StockOfAsync(productId));
    }

    [Fact]
    public async Task Concurrent_restocks_add_up_exactly()
    {
        var productId = await NewProductWithoutInventoryAsync();
        var admin = await fixture.AdminClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RestockAsync(admin, productId, 100)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(800, await StockOfAsync(productId));
        await AssertEveryProductMatchesItsLedgerAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    [InlineData(null)]
    public async Task Restock_rejects_grams_out_of_range(int? grams)
    {
        var product = await fixture.NewProductAsync(stockG: 1_000);
        var admin = await fixture.AdminClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await RestockAsync(admin, product.ProductId, grams)).StatusCode);
        Assert.Equal(1_000, await StockOfAsync(product.ProductId));
    }

    [Fact]
    public async Task Restock_beyond_the_stock_limit_is_unprocessable()
    {
        var product = await fixture.NewProductAsync(stockG: int.MaxValue - 10);
        var admin = await fixture.AdminClientAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await RestockAsync(admin, product.ProductId, 11)).StatusCode);
        Assert.Equal(int.MaxValue - 10, await StockOfAsync(product.ProductId));
    }

    [Fact]
    public async Task Unknown_product_is_not_found()
    {
        var admin = await fixture.AdminClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await RestockAsync(admin, 999_999, 100)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/admin/inventory/999999/movements")).StatusCode);
    }

    [Fact]
    public async Task Inventory_endpoints_require_the_admin_role()
    {
        var anonymous = fixture.Factory.CreateClient();
        var customer = await fixture.NewCustomerClientAsync();

        foreach (var client in new[] { anonymous, customer })
        {
            var expected = client == anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Assert.Equal(expected, (await client.GetAsync("/admin/inventory")).StatusCode);
            Assert.Equal(expected, (await RestockAsync(client, 1, 100)).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/admin/inventory/1/movements")).StatusCode);
        }
    }

    [Fact]
    public async Task Movements_are_paged_newest_first()
    {
        var product = await fixture.NewProductAsync(stockG: 5_000);
        var admin = await fixture.AdminClientAsync();
        await RestockAsync(admin, product.ProductId, 100);
        await RestockAsync(admin, product.ProductId, 200);
        await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)));

        var first = await admin.GetFromJsonAsync<MovementPageResponse>($"/admin/inventory/{product.ProductId}/movements?page=1&pageSize=3");
        var second = await admin.GetFromJsonAsync<MovementPageResponse>($"/admin/inventory/{product.ProductId}/movements?page=2&pageSize=3");

        Assert.Equal(4, first!.TotalCount);
        Assert.Equal([("Reserve", -500), ("Restock", 200), ("Restock", 100)], first.Items.Select(m => (m.Reason, m.DeltaG)));
        Assert.Equal([("Restock", 5_000)], second!.Items.Select(m => (m.Reason, m.DeltaG)));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/admin/inventory/{product.ProductId}/movements?pageSize=0")).StatusCode);
    }

    [Fact]
    public async Task Ten_parallel_orders_for_1000_g_sell_exactly_two_500_g_bags_in_every_round()
    {
        var customers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => fixture.NewCustomerClientAsync()));

        for (var round = 1; round <= 5; round++)
        {
            var product = await fixture.NewProductAsync(stockG: 1_000);

            var responses = await Task.WhenAll(customers.Select(customer =>
                PlaceAsync(customer, Body((product.Sku500, 1)), idempotencyKey: Guid.NewGuid().ToString())));

            var statuses = responses.Select(r => r.StatusCode).ToList();
            Assert.True(statuses.Count(s => s == HttpStatusCode.Created) == 2,
                $"Round {round}: expected 2 created, got {string.Join(", ", statuses)}.");
            Assert.Equal(8, statuses.Count(s => s == HttpStatusCode.Conflict));
            Assert.Equal(0, await StockOfAsync(product.ProductId));

            using var scope = fixture.Factory.Services.CreateScope();
            var reserved = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().InventoryMovements
                .Where(m => m.ProductId == product.ProductId && m.Reason == MovementReason.Reserve)
                .SumAsync(m => m.DeltaG);
            Assert.Equal(0 - 1_000, reserved);
        }
    }

    [Fact]
    public async Task Stock_equals_seed_plus_movements_after_a_mixed_sequence()
    {
        var first = await fixture.NewProductAsync(stockG: 10_000);
        var second = await fixture.NewProductAsync(stockG: 3_000);
        var admin = await fixture.AdminClientAsync();
        var alice = await fixture.NewCustomerClientAsync();
        var bob = await fixture.NewCustomerClientAsync();

        var a1 = await PlaceOkAsync(alice, Body((first.Sku1000, 2), (second.Sku500, 1)));
        var b1 = await PlaceOkAsync(bob, Body((first.Sku500, 3)));
        await alice.PostAsync($"/orders/{a1.Id}/pay", null);
        await RestockAsync(admin, second.ProductId, 4_000);
        await alice.PostAsync($"/orders/{a1.Id}/cancel", null);
        var b2 = await PlaceOkAsync(bob, Body((second.Sku1000, 5)));
        Assert.Equal(HttpStatusCode.Conflict, (await PlaceAsync(alice, Body((second.Sku1000, 3)))).StatusCode);
        await bob.PostAsync($"/orders/{b1.Id}/cancel", null);
        await RestockAsync(admin, first.ProductId, 1_500);
        await bob.PostAsync($"/orders/{b2.Id}/cancel", null);

        // first: 10000 - 2000 - 1500 + 2000 + 1500 + 1500; second: 3000 - 500 + 4000 + 500 - 5000 + 5000.
        Assert.Equal(10_000 + 1_500, await StockOfAsync(first.ProductId));
        Assert.Equal(3_000 + 4_000, await StockOfAsync(second.ProductId));
        await AssertEveryProductMatchesItsLedgerAsync();
    }
}
