using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Api;
using CoffeeShop.Application;
using CoffeeShop.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Api.Tests;

public class CatalogEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly HttpClient _client;

    public CatalogEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
        {
            services.AddSingleton<ICatalogReadStore, FakeCatalogStore>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        })).CreateClient();
    }

    [Fact]
    public async Task Products_include_their_skus()
    {
        var products = await _client.GetFromJsonAsync<ProductResponse[]>("/products");

        var product = Assert.Single(products!);
        var sku = Assert.Single(product.Skus);
        Assert.Equal("WholeBean", sku.Grind);
        Assert.Equal(500, sku.SizeG);
    }

    [Theory]
    [InlineData("2026-03-01", 12)]
    [InlineData("2026-06-01T00:00:00Z", 14)]
    [InlineData("2026-06-01T01:59:00%2B02:00", 12)]
    [InlineData("2026-05-31T23:59:59", 12)]
    [InlineData("2027-01-01", 15)]
    public async Task Price_resolves_as_of_the_requested_instant(string asOf, int expected)
    {
        var price = await _client.GetFromJsonAsync<PriceResponse>($"/skus/1/price?asOf={asOf}");

        Assert.Equal(expected, price!.Amount);
    }

    [Fact]
    public async Task Price_defaults_to_current_time()
    {
        var price = await _client.GetFromJsonAsync<PriceResponse>("/skus/1/price");

        Assert.Equal(14m, price!.Amount);
        Assert.Equal(Now, price.AsOf);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("01/02/2026")]
    public async Task Invalid_as_of_is_bad_request(string asOf)
    {
        var response = await _client.GetAsync($"/skus/1/price?asOf={asOf}");

        await AssertProblem(response, HttpStatusCode.BadRequest, "Invalid asOf");
    }

    [Fact]
    public async Task Unknown_sku_is_not_found()
    {
        var response = await _client.GetAsync("/skus/9999/price");

        await AssertProblem(response, HttpStatusCode.NotFound, "SKU not found");
    }

    [Fact]
    public async Task Sku_without_effective_price_is_not_found()
    {
        var response = await _client.GetAsync("/skus/1/price?asOf=2025-12-31");

        await AssertProblem(response, HttpStatusCode.NotFound, "No effective price");
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(title, problem!.Title);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCatalogStore : ICatalogReadStore
    {
        private static readonly EffectivePrice[] Prices =
        [
            new(1, 1, 12m, "EUR", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new(2, 1, 14m, "EUR", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            new(3, 1, 15m, "EUR", new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        ];

        public Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct)
        {
            var product = new Product(1, "Yirgacheffe", "Ethiopia", "Light");
            product.Skus.Add(new Sku(1, 1, Grind.WholeBean, 500));
            return Task.FromResult<IReadOnlyList<Product>>([product]);
        }

        public Task<bool> SkuExistsAsync(int skuId, CancellationToken ct) => Task.FromResult(skuId == 1);

        public Task<IReadOnlyList<EffectivePrice>> GetPricesAsync(int skuId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EffectivePrice>>(Prices);
    }
}
