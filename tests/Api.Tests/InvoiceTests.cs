using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CoffeeShop.Application.Messaging;
using CoffeeShop.Application.Storage;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Invoicing;
using CoffeeShop.Infrastructure.Messaging;
using CoffeeShop.Infrastructure.Persistence;
using CoffeeShop.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CoffeeShop.Api.Tests.OrderTestHelpers;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public partial class InvoiceTests(MsSqlFixture fixture)
{
    private readonly FakeClock _clock = new(DateTimeOffset.UtcNow.AddHours(1));

    private IFileStorage Storage => fixture.Factory.Services.GetRequiredService<IFileStorage>();

    private StorageScript StorageScript => fixture.Factory.Services.GetRequiredService<StorageScript>();

    private Task DrainAsync() => TestDispatcher.Create(fixture, _clock).DrainAsync();

    [GeneratedRegex(@"^INV-\d{4}-\d{6}$")]
    private static partial Regex InvoiceNumberFormat();

    private async Task<(HttpClient Customer, OrderResponse Order)> PlacePaidOrderAsync()
    {
        var product = await fixture.NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 2), (product.Sku1000, 1)));
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsync($"/orders/{order.Id}/pay", null)).StatusCode);
        return (customer, order);
    }

    private async Task<List<Invoice>> InvoicesForAsync(int orderId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Invoices
            .Where(i => i.OrderId == orderId).ToListAsync();
    }

    private async Task<OutboxMessage> PaidMessageAsync(int orderId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var paid = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().OutboxMessages
            .AsNoTracking().Where(m => m.Type == "OrderPaid").ToListAsync();
        return paid.Single(m => JsonDocument.Parse(m.PayloadJson).RootElement.GetProperty("orderId").GetInt32() == orderId);
    }

    [Fact]
    public async Task Paying_an_order_stores_its_invoice()
    {
        var (_, order) = await PlacePaidOrderAsync();

        await DrainAsync();

        var invoice = Assert.Single(await InvoicesForAsync(order.Id));
        Assert.Matches(InvoiceNumberFormat(), invoice.InvoiceNumber);
        Assert.Equal((Invoice.StorageKeyFor(order.Id), order.Total, order.Currency),
            (invoice.StorageKey, invoice.TotalSnapshot, invoice.Currency));
        Assert.True(await Storage.ExistsAsync(invoice.StorageKey, CancellationToken.None));

        var url = await Storage.GetDownloadUrlAsync(invoice.StorageKey, TimeSpan.FromMinutes(5), CancellationToken.None);
        using var http = new HttpClient();
        var pdf = await http.GetByteArrayAsync(url);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task Download_urls_are_signed_for_the_public_endpoint()
    {
        var (_, order) = await PlacePaidOrderAsync();
        await DrainAsync();
        var internalEndpoint = new Uri(fixture.MinioEndpoint);
        var publicEndpoint = new UriBuilder(internalEndpoint) { Host = "localhost" }.Uri;
        using var storage = new S3FileStorage(Options.Create(new StorageOptions
        {
            Endpoint = internalEndpoint.ToString(),
            PublicEndpoint = publicEndpoint.ToString(),
            Bucket = MsSqlFixture.InvoiceBucket,
            AccessKey = fixture.MinioAccessKey,
            SecretKey = fixture.MinioSecretKey,
        }), TimeProvider.System);

        var url = await storage.GetDownloadUrlAsync(Invoice.StorageKeyFor(order.Id), TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Equal(("localhost", publicEndpoint.Port), (url.Host, url.Port));
        using var http = new HttpClient();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(await http.GetByteArrayAsync(url), 0, 4));
    }

    [Fact]
    public async Task The_bucket_is_private()
    {
        var (_, order) = await PlacePaidOrderAsync();
        await DrainAsync();
        var url = await Storage.GetDownloadUrlAsync(Invoice.StorageKeyFor(order.Id), TimeSpan.FromMinutes(5), CancellationToken.None);

        using var http = new HttpClient();
        var unsigned = await http.GetAsync(url.GetLeftPart(UriPartial.Path));

        Assert.Equal(HttpStatusCode.Forbidden, unsigned.StatusCode);
    }

    [Fact]
    public async Task OrderPaid_delivered_twice_produces_one_invoice_and_one_number()
    {
        var (_, order) = await PlacePaidOrderAsync();
        await DrainAsync();
        var first = Assert.Single(await InvoicesForAsync(order.Id));
        var message = await PaidMessageAsync(order.Id);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            // Forget the inbox marker so the redelivery reaches the handler, which must notice the existing invoice.
            await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().ProcessedMessages
                .Where(p => p.MessageId == message.Id && p.HandlerName == typeof(InvoiceHandler).FullName)
                .ExecuteDeleteAsync();
        }
        var bus = fixture.Factory.Services.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(message.ToEnvelope(), CancellationToken.None);
        await bus.PublishAsync(message.ToEnvelope(), CancellationToken.None);

        var invoice = Assert.Single(await InvoicesForAsync(order.Id));
        Assert.Equal(first.InvoiceNumber, invoice.InvoiceNumber);
        Assert.Equal(1, StorageScript.Puts(invoice.StorageKey));
    }

    [Fact]
    public async Task Concurrent_handlers_for_one_order_store_one_invoice()
    {
        var (_, order) = await PlacePaidOrderAsync();
        var payload = (OrderPaid)MessageTypes.Deserialize((await PaidMessageAsync(order.Id)).ToEnvelope());
        var key = Invoice.StorageKeyFor(order.Id);
        StorageScript.DelayPuts(key, TimeSpan.FromSeconds(1));

        await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var handler = scope.ServiceProvider.GetServices<IMessageHandler<OrderPaid>>().OfType<InvoiceHandler>().Single();
            await handler.HandleAsync(payload, new MessageContext(Guid.NewGuid()), CancellationToken.None);
        }));

        // All three passed the existence check and uploaded; the two that lost the insert re-uploaded the winner's PDF.
        var invoice = Assert.Single(await InvoicesForAsync(order.Id));
        Assert.Equal(3 + 2, StorageScript.Puts(key));
        Assert.True(await Storage.ExistsAsync(invoice.StorageKey, CancellationToken.None));
    }

    [Fact]
    public async Task Order_cancelled_before_the_event_is_delivered_gets_no_invoice()
    {
        var (customer, order) = await PlacePaidOrderAsync();
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsync($"/orders/{order.Id}/cancel", null)).StatusCode);

        await DrainAsync();

        Assert.Empty(await InvoicesForAsync(order.Id));
        Assert.Equal(0, StorageScript.Puts(Invoice.StorageKeyFor(order.Id)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(8 * 24 * 3600)]
    public async Task Download_url_lifetime_must_be_positive_and_at_most_seven_days(int seconds)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Storage.GetDownloadUrlAsync("invoices/any.pdf", TimeSpan.FromSeconds(seconds), CancellationToken.None));
    }

    [Fact]
    public async Task Storage_failing_twice_then_succeeding_creates_the_invoice_on_the_third_attempt()
    {
        var (_, order) = await PlacePaidOrderAsync();
        var key = Invoice.StorageKeyFor(order.Id);
        StorageScript.FailPuts(key, 2);

        await DrainAsync();
        Assert.Empty(await InvoicesForAsync(order.Id));
        _clock.Advance(OutboxDispatcher.BackoffAfter(1));
        await DrainAsync();
        _clock.Advance(OutboxDispatcher.BackoffAfter(2));
        await DrainAsync();

        var message = await PaidMessageAsync(order.Id);
        using var scope = fixture.Factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().OutboxMessages
            .AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.Equal((OutboxStatus.Done, 3), (stored.Status, stored.Attempts));
        Assert.Contains("Scripted storage failure", stored.LastError);
        Assert.Single(await InvoicesForAsync(order.Id));
        Assert.Equal(3, StorageScript.Puts(key));
        Assert.True(await Storage.ExistsAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Paying_twice_creates_no_second_invoice()
    {
        var (customer, order) = await PlacePaidOrderAsync();
        await customer.PostAsync($"/orders/{order.Id}/pay", null);

        await DrainAsync();

        Assert.Single(await InvoicesForAsync(order.Id));
        Assert.Equal(1, StorageScript.Puts(Invoice.StorageKeyFor(order.Id)));
    }

    [Fact]
    public async Task Unpaid_orders_get_no_invoice()
    {
        var product = await fixture.NewProductAsync();
        var order = await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)));

        await DrainAsync();

        Assert.Empty(await InvoicesForAsync(order.Id));
        Assert.False(await Storage.ExistsAsync(Invoice.StorageKeyFor(order.Id), CancellationToken.None));
    }
}
