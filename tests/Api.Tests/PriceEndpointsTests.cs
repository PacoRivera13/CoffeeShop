using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public class PriceEndpointsTests(MsSqlFixture fixture)
{
    private const string CustomerPassword = "Str0ng!Passw0rd";
    private const decimal InitialAmount = 10m;

    private static readonly DateTimeOffset Today = new(DateTime.UtcNow.Date, TimeSpan.Zero);
    private static readonly DateTimeOffset Tomorrow = Today.AddDays(1);

    private readonly HttpClient _anonymous = fixture.Factory.CreateClient();

    private static string Iso(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:sszzz");

    /// <summary>Rows cannot be deleted and the database is shared, so every test prices its own SKU.</summary>
    private async Task<int> NewSkuAsync(bool withInitialPrice = true)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        var product = new Product(0, "Test product", "Test origin", "Medium");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var sku = new Sku(0, product.Id, Grind.Ground, 500);
        db.Skus.Add(sku);
        await db.SaveChangesAsync();

        if (withInitialPrice)
        {
            db.EffectivePrices.Add(new EffectivePrice(0, sku.Id, InitialAmount, "EUR", Today.AddDays(-30)));
            await db.SaveChangesAsync();
        }

        return sku.Id;
    }

    private async Task<HttpClient> AdminClientAsync() => await ClientForAsync(fixture.AdminEmail, fixture.AdminPassword);

    private async Task<HttpClient> CustomerClientAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var registered = await _anonymous.PostAsJsonAsync("/auth/register", new RegisterRequest(email, CustomerPassword, "Test"));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        return await ClientForAsync(email, CustomerPassword);
    }

    private async Task<HttpClient> ClientForAsync(string email, string password)
    {
        var login = await _anonymous.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> PostPrice(HttpClient client, int skuId, decimal amount, DateTimeOffset effectiveFrom) =>
        client.PostAsJsonAsync("/prices", new CreatePriceRequest(skuId, amount, "EUR", Iso(effectiveFrom)));

    private async Task<decimal> GetPriceAsync(int skuId, DateTimeOffset? asOf = null)
    {
        var query = asOf is null ? "" : $"?asOf={Uri.EscapeDataString(Iso(asOf.Value))}";
        return (await _anonymous.GetFromJsonAsync<PriceResponse>($"/skus/{skuId}/price{query}"))!.Amount;
    }

    [Fact]
    public async Task Creating_a_price_without_a_token_is_unauthorized()
    {
        var response = await PostPrice(_anonymous, 1, 5m, Tomorrow);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_price_as_customer_is_forbidden()
    {
        var response = await PostPrice(await CustomerClientAsync(), 1, 5m, Tomorrow);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_creates_a_price_and_gets_it_back()
    {
        var skuId = await NewSkuAsync();

        var response = await PostPrice(await AdminClientAsync(), skuId, 12.50m, Tomorrow);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PriceCreatedResponse>();
        Assert.True(created!.Id > 0);
        Assert.Equal(skuId, created.SkuId);
        Assert.Equal(12.50m, created.Amount);
        Assert.Equal("EUR", created.Currency);
        Assert.Equal(Tomorrow, created.EffectiveFrom);
    }

    [Fact]
    public async Task Duplicate_sku_and_date_is_a_conflict()
    {
        var skuId = await NewSkuAsync();
        var admin = await AdminClientAsync();
        await PostPrice(admin, skuId, 12m, Tomorrow);

        var response = await PostPrice(admin, skuId, 13m, Tomorrow);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Same_instant_with_another_offset_is_a_conflict()
    {
        var skuId = await NewSkuAsync();
        var admin = await AdminClientAsync();
        await PostPrice(admin, skuId, 12m, Tomorrow);

        var response = await PostPrice(admin, skuId, 13m, Tomorrow.ToOffset(TimeSpan.FromHours(2)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Simultaneous_duplicates_create_one_price()
    {
        var skuId = await NewSkuAsync();
        var admin = await AdminClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => PostPrice(admin, skuId, 12m + i, Tomorrow)));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Single(statuses, HttpStatusCode.Created);
        Assert.Equal(4, statuses.Count(s => s == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Past_date_is_unprocessable()
    {
        var skuId = await NewSkuAsync();

        var response = await PostPrice(await AdminClientAsync(), skuId, 12m, Today.AddSeconds(-1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Start_of_today_is_accepted()
    {
        var skuId = await NewSkuAsync();

        var response = await PostPrice(await AdminClientAsync(), skuId, 12m, Today);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_sku_is_not_found()
    {
        var response = await PostPrice(await AdminClientAsync(), 999_999, 12m, Tomorrow);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "EUR")]
    [InlineData(-1, "EUR")]
    [InlineData(1.999, "EUR")]
    [InlineData(1, "eur")]
    [InlineData(1, "EURO")]
    [InlineData(1, "EUX")]
    [InlineData(1, "")]
    public async Task Invalid_amount_or_currency_is_bad_request(double amount, string currency)
    {
        var skuId = await NewSkuAsync();
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/prices", new CreatePriceRequest(skuId, (decimal)amount, currency, Iso(Tomorrow)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, 1, "EUR", "2099-01-01")]
    [InlineData(1, null, "EUR", "2099-01-01")]
    [InlineData(1, 1, null, "2099-01-01")]
    [InlineData(1, 1, "EUR", null)]
    [InlineData(1, 1, "EUR", "abc")]
    [InlineData(1, 1, "EUR", "01/02/2099")]
    public async Task Missing_or_malformed_fields_are_bad_request(int? skuId, int? amount, string? currency, string? effectiveFrom)
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/prices", new CreatePriceRequest(skuId, amount, currency, effectiveFrom));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tomorrows_price_does_not_change_todays_price_but_applies_as_of_tomorrow()
    {
        var skuId = await NewSkuAsync();

        await PostPrice(await AdminClientAsync(), skuId, 20m, Tomorrow);

        Assert.Equal(InitialAmount, await GetPriceAsync(skuId));
        Assert.Equal(20m, await GetPriceAsync(skuId, Tomorrow));
    }

    [Fact]
    public async Task History_is_ordered_latest_first_and_marks_the_current_price()
    {
        var skuId = await NewSkuAsync();
        var admin = await AdminClientAsync();
        await PostPrice(admin, skuId, 40m, Tomorrow.AddDays(10));
        await PostPrice(admin, skuId, 20m, Today);
        await PostPrice(admin, skuId, 30m, Tomorrow);

        var history = await admin.GetFromJsonAsync<PriceHistoryResponse[]>($"/skus/{skuId}/prices");

        Assert.NotNull(history);
        Assert.Equal([40m, 30m, 20m, InitialAmount], history.Select(p => p.Amount));
        Assert.Equal([false, false, true, false], history.Select(p => p.IsCurrent));
    }

    [Fact]
    public async Task History_has_no_current_price_when_none_applies_yet()
    {
        var skuId = await NewSkuAsync(withInitialPrice: false);
        var admin = await AdminClientAsync();
        await PostPrice(admin, skuId, 20m, Tomorrow);

        var history = await admin.GetFromJsonAsync<PriceHistoryResponse[]>($"/skus/{skuId}/prices");

        Assert.False(Assert.Single(history!).IsCurrent);
    }

    [Fact]
    public async Task History_of_a_sku_without_prices_is_empty()
    {
        var skuId = await NewSkuAsync(withInitialPrice: false);
        var admin = await AdminClientAsync();

        Assert.Empty((await admin.GetFromJsonAsync<PriceHistoryResponse[]>($"/skus/{skuId}/prices"))!);
    }

    [Fact]
    public async Task History_requires_the_admin_role()
    {
        var customer = await CustomerClientAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/skus/1/prices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/skus/1/prices")).StatusCode);
    }

    [Fact]
    public async Task History_of_an_unknown_sku_is_not_found()
    {
        var admin = await AdminClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/skus/999999/prices")).StatusCode);
    }

    [Fact]
    public async Task Current_price_stays_anonymous()
    {
        Assert.Equal(InitialAmount, await GetPriceAsync(await NewSkuAsync()));
    }

    [Fact]
    public async Task Prices_cannot_be_updated_or_deleted()
    {
        var admin = await AdminClientAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PutAsJsonAsync("/prices", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync("/prices")).StatusCode);
    }
}
