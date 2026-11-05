using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public class OrderEndpointsTests(MsSqlFixture fixture)
{
    private const decimal Price500 = 10m;
    private const decimal Price1000 = 18m;

    private static DateTimeOffset Today => new(DateTime.UtcNow.Date, TimeSpan.Zero);

    private sealed record TestProduct(int ProductId, int Sku500, int Sku1000);

    /// <summary>Rows cannot be deleted and the database is shared, so every test orders its own product.</summary>
    private async Task<TestProduct> NewProductAsync(int stockG = 20_000, string currency = "EUR")
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

    private async Task<(int StockG, int MovementSum, List<InventoryMovement> Movements)> InventoryOfAsync(int productId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        var stock = await db.Inventories.Where(i => i.ProductId == productId).Select(i => i.StockG).SingleAsync();
        var movements = await db.InventoryMovements.Where(m => m.ProductId == productId).OrderBy(m => m.Id).ToListAsync();
        return (stock, movements.Sum(m => m.DeltaG), movements);
    }

    private static object Body(params (int SkuId, int Qty)[] items) =>
        new { items = items.Select(i => new { skuId = i.SkuId, qty = i.Qty }) };

    private static Task<HttpResponseMessage> PlaceAsync(HttpClient client, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/orders") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static async Task<OrderResponse> PlaceOkAsync(HttpClient client, object body, string? idempotencyKey = null)
    {
        var response = await PlaceAsync(client, body, idempotencyKey);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }

    [Fact]
    public async Task Order_snapshots_prices_computes_the_total_and_reserves_stock()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();

        var response = await PlaceAsync(customer, Body((product.Sku500, 2), (product.Sku1000, 1)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        Assert.Equal($"/orders/{order.Id}", response.Headers.Location?.ToString());
        Assert.Equal("Pending", order.Status);
        Assert.Equal("EUR", order.Currency);
        Assert.Equal(2 * Price500 + Price1000, order.Total);
        Assert.Equal([(product.Sku500, 2, Price500), (product.Sku1000, 1, Price1000)], order.Lines.Select(l => (l.SkuId, l.Qty, l.UnitPrice)));
        Assert.All(order.Lines, l => Assert.True(l.PriceId > 0));

        var inventory = await InventoryOfAsync(product.ProductId);
        Assert.Equal(20_000 - 2_000, inventory.StockG);
        Assert.Equal(inventory.StockG, inventory.MovementSum);
        var reserve = inventory.Movements.Last();
        Assert.Equal((MovementReason.Reserve, -2_000, order.Id), (reserve.Reason, reserve.DeltaG, reserve.OrderId));
    }

    [Fact]
    public async Task Duplicate_skus_are_merged_into_one_line()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();

        var order = await PlaceOkAsync(customer, Body((product.Sku500, 2), (product.Sku500, 3)));

        var line = Assert.Single(order.Lines);
        Assert.Equal(5, line.Qty);
        Assert.Equal(5 * Price500, order.Total);
    }

    [Fact]
    public async Task Price_change_after_ordering_does_not_alter_the_order()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 1)));

        var admin = await fixture.AdminClientAsync();
        var priceChange = await admin.PostAsJsonAsync("/prices", new CreatePriceRequest(product.Sku500, 99m, "EUR", Today.ToString("yyyy-MM-dd")));
        Assert.Equal(HttpStatusCode.Created, priceChange.StatusCode);

        var reloaded = await customer.GetFromJsonAsync<OrderResponse>($"/orders/{order.Id}");
        Assert.Equal(Price500, Assert.Single(reloaded!.Lines).UnitPrice);
        Assert.Equal(Price500, reloaded.Total);
        Assert.Equal(99m, (await PlaceOkAsync(customer, Body((product.Sku500, 1)))).Total);
    }

    [Fact]
    public async Task Insufficient_stock_is_a_conflict_and_leaves_stock_unchanged()
    {
        var product = await NewProductAsync(stockG: 1_500);
        var customer = await fixture.NewCustomerClientAsync();

        var response = await PlaceAsync(customer, Body((product.Sku1000, 2)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(product.ProductId, problem.GetProperty("productId").GetInt32());
        Assert.Equal(1_500, problem.GetProperty("availableG").GetInt32());
        Assert.Equal(2_000, problem.GetProperty("requiredG").GetInt32());

        var inventory = await InventoryOfAsync(product.ProductId);
        Assert.Equal(1_500, inventory.StockG);
        Assert.Single(inventory.Movements);
    }

    [Fact]
    public async Task Shortage_on_one_product_rolls_back_the_reservation_of_the_others()
    {
        var plenty = await NewProductAsync();
        var scarce = await NewProductAsync(stockG: 500);
        var customer = await fixture.NewCustomerClientAsync();

        var response = await PlaceAsync(customer, Body((plenty.Sku1000, 3), (scarce.Sku1000, 1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(20_000, (await InventoryOfAsync(plenty.ProductId)).StockG);
        Assert.Equal(500, (await InventoryOfAsync(scarce.ProductId)).StockG);
        Assert.Equal(0, (await customer.GetFromJsonAsync<OrderPageResponse>("/orders"))!.TotalCount);
    }

    [Fact]
    public async Task Concurrent_orders_never_oversell()
    {
        var product = await NewProductAsync(stockG: 2_000);
        var customer = await fixture.NewCustomerClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PlaceAsync(customer, Body((product.Sku1000, 1)))));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var inventory = await InventoryOfAsync(product.ProductId);
        Assert.Equal(0, inventory.StockG);
        Assert.Equal(0, inventory.MovementSum);
    }

    [Fact]
    public async Task Mixed_currencies_are_unprocessable_and_reserve_nothing()
    {
        var euros = await NewProductAsync();
        var dollars = await NewProductAsync(currency: "USD");
        var customer = await fixture.NewCustomerClientAsync();

        var response = await PlaceAsync(customer, Body((euros.Sku500, 1), (dollars.Sku500, 1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(20_000, (await InventoryOfAsync(euros.ProductId)).StockG);
        Assert.Equal(20_000, (await InventoryOfAsync(dollars.ProductId)).StockG);
    }

    [Fact]
    public async Task Unknown_sku_is_not_found()
    {
        var customer = await fixture.NewCustomerClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await PlaceAsync(customer, Body((999_999, 1)))).StatusCode);
    }

    public static TheoryData<object> InvalidBodies => new()
    {
        new { items = Array.Empty<object>() },
        new { },
        new { items = Enumerable.Range(1, 21).Select(i => new { skuId = i, qty = 1 }) },
        new { items = new[] { new { skuId = 1, qty = 0 } } },
        new { items = new[] { new { skuId = 1, qty = 51 } } },
        new { items = new[] { new { skuId = 1, qty = 30 }, new { skuId = 1, qty = 30 } } },
        new { items = new[] { new { skuId = 1 } } },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Invalid_order_is_bad_request(object body)
    {
        var customer = await fixture.NewCustomerClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await PlaceAsync(customer, body)).StatusCode);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_original_order_without_side_effects()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var key = Guid.NewGuid().ToString();
        var original = await PlaceOkAsync(customer, Body((product.Sku500, 2)), key);

        var retry = await PlaceAsync(customer, Body((product.Sku1000, 5)), key);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(original, await retry.Content.ReadFromJsonAsync<OrderResponse>(), OrderComparer.Instance);
        var inventory = await InventoryOfAsync(product.ProductId);
        Assert.Equal(20_000 - 1_000, inventory.StockG);
        Assert.Single(inventory.Movements, m => m.Reason == MovementReason.Reserve);
    }

    [Fact]
    public async Task Retry_with_an_invalid_body_still_returns_the_original_order()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var key = Guid.NewGuid().ToString();
        var original = await PlaceOkAsync(customer, Body((product.Sku500, 1)), key);

        var retry = await PlaceAsync(customer, Body((product.Sku500, 51)), key);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(original.Id, (await retry.Content.ReadFromJsonAsync<OrderResponse>())!.Id);
    }

    [Fact]
    public async Task Page_beyond_the_last_is_empty()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        await PlaceOkAsync(customer, Body((product.Sku500, 1)));

        var page = await customer.GetFromJsonAsync<OrderPageResponse>($"/orders?page={int.MaxValue}&pageSize=100");

        Assert.Empty(page!.Items);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Idempotency_keys_are_scoped_to_the_customer()
    {
        var product = await NewProductAsync();
        var key = Guid.NewGuid().ToString();
        var first = await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)), key);

        var second = await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)), key);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Concurrent_retries_with_one_idempotency_key_create_one_order()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PlaceAsync(customer, Body((product.Sku500, 1)), key)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<OrderResponse>())!.Id));
        Assert.Single(ids.Distinct());
        Assert.Equal(20_000 - 500, (await InventoryOfAsync(product.ProductId)).StockG);
    }

    [Fact]
    public async Task Cancel_restores_stock_exactly_and_is_idempotent()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 3), (product.Sku1000, 2)));

        var cancelled = await customer.PostAsync($"/orders/{order.Id}/cancel", null);
        var again = await customer.PostAsync($"/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<OrderResponse>())!.Status);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var inventory = await InventoryOfAsync(product.ProductId);
        Assert.Equal(20_000, inventory.StockG);
        Assert.Equal(20_000, inventory.MovementSum);
        Assert.Equal([MovementReason.Restock, MovementReason.Reserve, MovementReason.Release], inventory.Movements.Select(m => m.Reason));
        Assert.Equal(3_500, inventory.Movements.Last().DeltaG);
    }

    [Fact]
    public async Task Concurrent_cancels_release_stock_once()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku1000, 4)));

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => customer.PostAsync($"/orders/{order.Id}/cancel", null)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var inventory = await InventoryOfAsync(product.ProductId);
        Assert.Equal(20_000, inventory.StockG);
        Assert.Single(inventory.Movements, m => m.Reason == MovementReason.Release);
    }

    [Fact]
    public async Task Pay_moves_to_paid_and_is_idempotent_and_a_paid_order_can_still_be_cancelled()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 1)));

        var paid = await customer.PostAsync($"/orders/{order.Id}/pay", null);
        var again = await customer.PostAsync($"/orders/{order.Id}/pay", null);
        var cancelled = await customer.PostAsync($"/orders/{order.Id}/cancel", null);
        var payCancelled = await customer.PostAsync($"/orders/{order.Id}/pay", null);

        Assert.Equal("Paid", (await paid.Content.ReadFromJsonAsync<OrderResponse>())!.Status);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, payCancelled.StatusCode);
        Assert.Equal(20_000, (await InventoryOfAsync(product.ProductId)).StockG);
    }

    [Fact]
    public async Task Shipped_order_cannot_be_cancelled()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 1)));
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            var tracked = await db.Orders.SingleAsync(o => o.Id == order.Id);
            tracked.Pay();
            tracked.StartPreparing();
            tracked.Ship();
            await db.SaveChangesAsync();
        }

        var response = await customer.PostAsync($"/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(20_000 - 500, (await InventoryOfAsync(product.ProductId)).StockG);
    }

    [Fact]
    public async Task Another_customers_order_is_not_found()
    {
        var product = await NewProductAsync();
        var order = await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)));
        var stranger = await fixture.NewCustomerClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/orders/{order.Id}/pay", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/orders/{order.Id}/cancel", null)).StatusCode);
        Assert.Equal(20_000 - 500, (await InventoryOfAsync(product.ProductId)).StockG);
    }

    [Fact]
    public async Task Admin_can_read_any_order_but_cannot_place_one()
    {
        var product = await NewProductAsync();
        var order = await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)));
        var admin = await fixture.AdminClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PlaceAsync(admin, Body((product.Sku500, 1)))).StatusCode);
    }

    [Fact]
    public async Task Orders_require_authentication()
    {
        var anonymous = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await PlaceAsync(anonymous, Body((1, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/orders/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/orders/1/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Own_orders_are_listed_newest_first_and_paged()
    {
        var product = await NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var ids = new List<int>();
        for (var i = 0; i < 3; i++)
            ids.Add((await PlaceOkAsync(customer, Body((product.Sku500, 1)))).Id);
        await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)));

        var first = await customer.GetFromJsonAsync<OrderPageResponse>("/orders?page=1&pageSize=2");
        var second = await customer.GetFromJsonAsync<OrderPageResponse>("/orders?page=2&pageSize=2");

        Assert.Equal(3, first!.TotalCount);
        Assert.Equal([ids[2], ids[1]], first.Items.Select(o => o.Id));
        Assert.Equal([ids[0]], second!.Items.Select(o => o.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.GetAsync("/orders?pageSize=0")).StatusCode);
    }

    private sealed class OrderComparer : IEqualityComparer<OrderResponse?>
    {
        public static readonly OrderComparer Instance = new();

        public bool Equals(OrderResponse? x, OrderResponse? y) =>
            x is not null && y is not null
            && (x.Id, x.Status, x.Total, x.Currency, x.CreatedAt) == (y.Id, y.Status, y.Total, y.Currency, y.CreatedAt)
            && x.Lines.SequenceEqual(y.Lines);

        public int GetHashCode(OrderResponse? obj) => obj?.Id ?? 0;
    }
}
