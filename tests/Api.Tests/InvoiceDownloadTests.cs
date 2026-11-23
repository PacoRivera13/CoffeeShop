using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Messaging;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CoffeeShop.Api.Tests.OrderTestHelpers;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public class InvoiceDownloadTests(MsSqlFixture fixture)
{
    private readonly FakeClock _clock = new(DateTimeOffset.UtcNow.AddHours(1));

    private Task DrainAsync() => TestDispatcher.Create(fixture, _clock).DrainAsync();

    private async Task<(HttpClient Customer, OrderResponse Order)> PlaceOrderAsync(bool pay)
    {
        var product = await fixture.NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 1)));
        if (pay)
            Assert.Equal(HttpStatusCode.OK, (await customer.PostAsync($"/orders/{order.Id}/pay", null)).StatusCode);
        return (customer, order);
    }

    private static Task<HttpResponseMessage> GetInvoiceAsync(HttpClient client, int orderId) =>
        client.GetAsync($"/orders/{orderId}/invoice");

    private static async Task<InvoiceResponse> ReadyInvoiceAsync(HttpClient client, int orderId)
    {
        var response = await GetInvoiceAsync(client, orderId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    [Fact]
    public async Task Invoice_requires_authentication()
    {
        var (_, order) = await PlaceOrderAsync(pay: true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetInvoiceAsync(fixture.Factory.CreateClient(), order.Id)).StatusCode);
    }

    [Fact]
    public async Task Another_customers_invoice_is_not_found()
    {
        var (_, order) = await PlaceOrderAsync(pay: true);
        await DrainAsync();

        var stranger = await fixture.NewCustomerClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await GetInvoiceAsync(stranger, order.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetInvoiceAsync(stranger, 999_999)).StatusCode);
    }

    [Fact]
    public async Task Unpaid_order_has_no_invoice()
    {
        var (customer, order) = await PlaceOrderAsync(pay: false);
        await DrainAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await GetInvoiceAsync(customer, order.Id)).StatusCode);
    }

    [Fact]
    public async Task Paid_order_answers_202_until_the_dispatcher_generates_the_invoice()
    {
        var (customer, order) = await PlaceOrderAsync(pay: true);

        var pending = await GetInvoiceAsync(customer, order.Id);

        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(2), pending.Headers.RetryAfter?.Delta);
        Assert.Equal("pending", JsonDocument.Parse(await pending.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());

        await DrainAsync();

        var invoice = await ReadyInvoiceAsync(customer, order.Id);
        using var http = new HttpClient();
        var pdf = await http.GetByteArrayAsync(invoice.Url);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task Ready_invoice_exposes_only_a_short_lived_public_url()
    {
        var (customer, order) = await PlaceOrderAsync(pay: true);
        await DrainAsync();

        var response = await GetInvoiceAsync(customer, order.Id);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var invoice = await ReadyInvoiceAsync(customer, order.Id);

        Assert.Equal(["expiresAt", "invoiceNumber", "url"], json.EnumerateObject().Select(p => p.Name).Order());
        var url = new Uri(invoice.Url);
        Assert.Equal(new Uri(fixture.PublicMinioEndpoint).Authority, url.Authority);
        Assert.NotEqual(new Uri(fixture.MinioEndpoint).Host, url.Host);
        Assert.Matches(@"^INV-\d{4}-\d{6}$", invoice.InvoiceNumber);
        var lifetime = invoice.ExpiresAt - DateTimeOffset.UtcNow;
        Assert.InRange(lifetime, TimeSpan.FromMinutes(4.5), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Admin_can_fetch_any_orders_invoice()
    {
        var (customer, order) = await PlaceOrderAsync(pay: true);
        await DrainAsync();
        var admin = await fixture.AdminClientAsync();

        var asAdmin = await ReadyInvoiceAsync(admin, order.Id);
        var asOwner = await ReadyInvoiceAsync(customer, order.Id);

        Assert.Equal(asOwner.InvoiceNumber, asAdmin.InvoiceNumber);
    }

    [Fact]
    public async Task Order_cancelled_after_invoicing_keeps_its_invoice()
    {
        var (customer, order) = await PlaceOrderAsync(pay: true);
        await DrainAsync();
        var before = await ReadyInvoiceAsync(customer, order.Id);

        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsync($"/orders/{order.Id}/cancel", null)).StatusCode);
        await DrainAsync();

        var after = await ReadyInvoiceAsync(customer, order.Id);
        Assert.Equal(before.InvoiceNumber, after.InvoiceNumber);
    }

    [Fact]
    public async Task Paid_order_whose_invoice_generation_failed_is_a_conflict_not_an_endless_202()
    {
        var (customer, order) = await PlaceOrderAsync(pay: true);
        fixture.Factory.Services.GetRequiredService<StorageScript>().FailPuts(Invoice.StorageKeyFor(order.Id), int.MaxValue);

        for (var attempt = 0; attempt < TestDispatcher.MaxAttempts; attempt++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await GetInvoiceAsync(customer, order.Id)).StatusCode);
            await DrainAsync();
            _clock.Advance(TimeSpan.FromMinutes(6));
        }

        Assert.Equal(HttpStatusCode.Conflict, (await GetInvoiceAsync(customer, order.Id)).StatusCode);
    }

    [Fact]
    public async Task Order_paid_before_invoicing_existed_is_a_conflict_not_an_endless_202()
    {
        var (customer, order) = await PlaceOrderAsync(pay: true);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            // An OrderPaid delivered before the invoice handler existed: Done, but no invoice was ever made.
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            var ids = (await db.OutboxMessages.AsNoTracking().Where(m => m.Type == "OrderPaid").ToListAsync())
                .Where(m => JsonDocument.Parse(m.PayloadJson).RootElement.GetProperty("orderId").GetInt32() == order.Id)
                .Select(m => m.Id)
                .ToList();
            await db.OutboxMessages.Where(m => ids.Contains(m.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxStatus.Done));
        }

        Assert.Equal(HttpStatusCode.Conflict, (await GetInvoiceAsync(customer, order.Id)).StatusCode);
    }

    [Fact]
    public async Task Order_cancelled_before_invoicing_never_gets_one()
    {
        var (customer, order) = await PlaceOrderAsync(pay: true);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsync($"/orders/{order.Id}/cancel", null)).StatusCode);
        await DrainAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await GetInvoiceAsync(customer, order.Id)).StatusCode);
    }
}
