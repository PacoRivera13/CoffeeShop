using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using CoffeeShop.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static CoffeeShop.Api.Tests.OrderTestHelpers;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public class DemoSeederTests(MsSqlFixture fixture)
{
    private static readonly string[] DemoProducts =
        ["Chiapas Altura", "Veracruz Pluma", "Oaxaca Pluma Hidalgo", "Huehuetenango", "Yirgacheffe", "Cerrado"];

    private async Task<T> WithDbAsync<T>(Func<CoffeeShopDbContext, Task<T>> query)
    {
        using var scope = (await fixture.DemoFactoryAsync()).Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>());
    }

    /// <summary>The demo row of a product; the migration seed has its own Yirgacheffe with a lower id.</summary>
    private Task<Product> DemoProductAsync(string name) =>
        WithDbAsync(db => db.Products.Include(p => p.Skus).Where(p => p.Name == name).OrderByDescending(p => p.Id).FirstAsync());

    private Task<int[]> CountsAsync() => WithDbAsync(async db => new[]
    {
        await db.Products.CountAsync(), await db.Skus.CountAsync(), await db.EffectivePrices.CountAsync(),
        await db.Inventories.CountAsync(), await db.InventoryMovements.CountAsync(), await db.Orders.CountAsync(),
        await db.OutboxMessages.CountAsync(), await db.Customers.CountAsync(), await db.Users.CountAsync(),
    });

    [Fact]
    public async Task Seeds_the_catalogue_stock_customer_and_orders()
    {
        var stock = new Dictionary<string, int>();
        foreach (var name in DemoProducts)
        {
            var productId = (await DemoProductAsync(name)).Id;
            stock[name] = await WithDbAsync(db => db.Inventories.Where(i => i.ProductId == productId).Select(i => i.StockG).SingleAsync());
        }

        // Orders use Chiapas (2×500 + 500), Veracruz (1000 reserved; 500 released) and Oaxaca (500) only.
        Assert.Equal(13_500, stock["Chiapas Altura"]);
        Assert.Equal(11_000, stock["Veracruz Pluma"]);
        Assert.Equal(7_500, stock["Oaxaca Pluma Hidalgo"]);
        Assert.Equal(6_000, stock["Huehuetenango"]);
        Assert.Equal(1_500, stock["Yirgacheffe"]);
        Assert.Equal(400, stock["Cerrado"]);

        foreach (var name in DemoProducts)
            Assert.Equal(4, (await DemoProductAsync(name)).Skus.Count);

        var statuses = await WithDbAsync(db => db.Customers.Where(c => c.Email == MsSqlFixture.DemoCustomerEmail)
            .Join(db.Orders, c => c.Id, o => o.CustomerId, (_, o) => o.Status).ToListAsync());
        Assert.Equal(
            [OrderStatus.Pending, OrderStatus.Paid, OrderStatus.Delivered, OrderStatus.Cancelled],
            statuses.Order());
    }

    [Fact]
    public async Task A_second_run_changes_nothing()
    {
        var factory = await fixture.DemoFactoryAsync();
        var before = await CountsAsync();

        await factory.Services.SeedDemoAsync(
            factory.Services.GetRequiredService<IConfiguration>(), factory.Services.GetRequiredService<IHostEnvironment>());

        Assert.Equal(before, await CountsAsync());
    }

    [Fact]
    public void Refuses_to_start_outside_Development()
    {
        using var factory = fixture.CreateFactory(("Seed:Demo", "true"))
            .WithWebHostBuilder(host => host.UseEnvironment(Environments.Production));

        var error = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("Seed__Demo=true is only allowed", Flatten(error));
    }

    [Fact]
    public async Task Stock_equals_the_sum_of_the_movement_ledger_for_every_product()
    {
        var mismatches = await WithDbAsync(db => db.Inventories
            .Select(i => new { i.ProductId, i.StockG, Ledger = db.InventoryMovements.Where(m => m.ProductId == i.ProductId).Sum(m => m.DeltaG) })
            .Where(x => x.StockG != x.Ledger)
            .ToListAsync());

        Assert.Empty(mismatches);
    }

    [Fact]
    public async Task Every_demo_sku_has_a_current_price()
    {
        var client = (await fixture.DemoFactoryAsync()).CreateClient();

        foreach (var name in DemoProducts)
            foreach (var sku in (await DemoProductAsync(name)).Skus)
            {
                var price = await client.GetFromJsonAsync<PriceResponse>($"/skus/{sku.Id}/price");
                Assert.Equal("MXN", price!.Currency);
            }
    }

    [Fact]
    public async Task Yirgacheffe_price_rise_is_scheduled_for_next_week()
    {
        var client = (await fixture.DemoFactoryAsync()).CreateClient();
        var sku = (await DemoProductAsync("Yirgacheffe")).Skus.Single(s => s is { Grind: Grind.WholeBean, SizeG: 500 });

        var today = await client.GetFromJsonAsync<PriceResponse>($"/skus/{sku.Id}/price");
        var asOf = Uri.EscapeDataString(Today.AddDays(8).ToString("O"));
        var nextWeek = await client.GetFromJsonAsync<PriceResponse>($"/skus/{sku.Id}/price?asOf={asOf}");

        Assert.Equal(259m, today!.Amount);
        Assert.Equal(284.90m, nextWeek!.Amount);
    }

    [Fact]
    public async Task Chiapas_keeps_its_older_cheaper_price()
    {
        var client = (await fixture.DemoFactoryAsync()).CreateClient();
        var sku = (await DemoProductAsync("Chiapas Altura")).Skus.Single(s => s is { Grind: Grind.Ground, SizeG: 500 });

        var asOf = Uri.EscapeDataString(Today.AddDays(-200).ToString("O"));
        var past = await client.GetFromJsonAsync<PriceResponse>($"/skus/{sku.Id}/price?asOf={asOf}");

        Assert.Equal(169m, past!.Amount);
    }

    [Fact]
    public async Task Ordering_a_500_g_bag_of_Cerrado_is_refused_for_lack_of_stock()
    {
        var factory = await fixture.DemoFactoryAsync();
        var customer = await MsSqlFixture.ClientForAsync(factory, MsSqlFixture.DemoCustomerEmail, MsSqlFixture.DemoCustomerPassword);
        var sku = (await DemoProductAsync("Cerrado")).Skus.Single(s => s is { Grind: Grind.WholeBean, SizeG: 500 });

        var response = await PlaceAsync(customer, Body((sku.Id, 1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static string Flatten(Exception error) =>
        error.InnerException is null ? error.Message : $"{error.Message} {Flatten(error.InnerException)}";
}
