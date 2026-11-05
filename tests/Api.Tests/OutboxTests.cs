using System.Net;
using System.Text.Json;
using CoffeeShop.Application.Messaging;
using CoffeeShop.Infrastructure.Messaging;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static CoffeeShop.Api.Tests.OrderTestHelpers;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public class OutboxTests(MsSqlFixture fixture)
{
    private const int MaxAttempts = 5;

    // Ahead of the real clock, so messages written by the API during the test are already due.
    private readonly FakeClock _clock = new(DateTimeOffset.UtcNow.AddHours(1));

    private HandlerScript Script => fixture.Factory.Services.GetRequiredService<HandlerScript>();

    private CapturedNotifications Emails => fixture.Factory.Services.GetRequiredService<CapturedNotifications>();

    private OutboxDispatcher NewDispatcher(int batchSize = 100) => new(
        fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
        _clock,
        Options.Create(new OutboxOptions { BatchSize = batchSize, MaxAttempts = MaxAttempts }),
        NullLogger<OutboxDispatcher>.Instance);

    /// <summary>Dispatches until nothing is due; the shared database may also hold other tests' messages.</summary>
    private static async Task<int> DrainAsync(OutboxDispatcher dispatcher)
    {
        var total = 0;
        for (int claimed; (claimed = await dispatcher.DispatchOnceAsync(CancellationToken.None)) > 0;)
            total += claimed;
        return total;
    }

    private Task DrainAsync() => DrainAsync(NewDispatcher());

    private async Task<List<OutboxMessage>> MessagesForOrderAsync(int orderId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var messages = await db.OutboxMessages.AsNoTracking().OrderBy(m => m.OccurredAt).ThenBy(m => m.Id).ToListAsync();

        return messages
            .Where(m => JsonDocument.Parse(m.PayloadJson).RootElement.GetProperty("orderId").GetInt32() == orderId)
            .ToList();
    }

    private async Task<OutboxMessage> SingleMessageAsync(int orderId) => Assert.Single(await MessagesForOrderAsync(orderId));

    private async Task<int> OutboxCountAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().OutboxMessages.CountAsync();
    }

    private async Task<OrderResponse> PlaceOrderAsync()
    {
        var product = await fixture.NewProductAsync();
        return await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)));
    }

    [Fact]
    public async Task Created_order_is_published_in_its_transaction_and_delivered_once()
    {
        var order = await PlaceOrderAsync();

        var pending = await SingleMessageAsync(order.Id);
        Assert.Equal(("OrderCreated", OutboxStatus.Pending, 0), (pending.Type, pending.Status, pending.Attempts));
        var payload = (OrderCreated)MessageTypes.Deserialize(pending.ToEnvelope());
        Assert.Equal((order.Id, order.Total, order.Currency), (payload.OrderId, payload.Total, payload.Currency));

        await DrainAsync();

        var done = await SingleMessageAsync(order.Id);
        Assert.Equal((OutboxStatus.Done, 1), (done.Status, done.Attempts));
        Assert.NotNull(done.ProcessedAt);
        Assert.Equal(1, Emails.Count("OrderCreated", order.Id));
        Assert.Equal(1, Script.Runs(order.Id));

        using var scope = fixture.Factory.Services.CreateScope();
        var processed = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().ProcessedMessages
            .Where(p => p.MessageId == done.Id).Select(p => p.HandlerName).ToListAsync();
        Assert.Equal(
            new HashSet<string> { typeof(NotificationHandler).FullName!, typeof(ScriptedHandler).FullName! },
            processed.ToHashSet());
        Assert.Equal(2, processed.Count);
    }

    [Fact]
    public async Task Pay_and_cancel_publish_once_each_and_no_ops_publish_nothing()
    {
        var product = await fixture.NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 1)));

        foreach (var action in new[] { "pay", "pay", "cancel", "cancel" })
            Assert.Equal(HttpStatusCode.OK, (await customer.PostAsync($"/orders/{order.Id}/{action}", null)).StatusCode);
        await DrainAsync();

        var messages = await MessagesForOrderAsync(order.Id);
        Assert.Equal(["OrderCreated", "OrderPaid", "OrderCancelled"], messages.Select(m => m.Type));
        Assert.All(messages, m => Assert.Equal(OutboxStatus.Done, m.Status));
        Assert.Equal(1, Emails.Count("OrderPaid", order.Id));
        Assert.Equal(1, Emails.Count("OrderCancelled", order.Id));
    }

    [Fact]
    public async Task Redelivered_message_runs_each_handler_once()
    {
        var order = await PlaceOrderAsync();
        await DrainAsync();
        var message = await SingleMessageAsync(order.Id);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            // Simulates a dispatcher that crashed after the handlers ran but before marking the message Done.
            await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().OutboxMessages
                .Where(m => m.Id == message.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxStatus.Pending).SetProperty(m => m.NextAttemptAt, _clock.GetUtcNow()));
        }
        await DrainAsync();
        await fixture.Factory.Services.GetRequiredService<IMessageBus>().PublishAsync(message.ToEnvelope(), CancellationToken.None);

        var redelivered = await SingleMessageAsync(order.Id);
        Assert.Equal((OutboxStatus.Done, 2), (redelivered.Status, redelivered.Attempts));
        Assert.Equal(1, Emails.Count("OrderCreated", order.Id));
        Assert.Equal(1, Script.Runs(order.Id));
    }

    [Fact]
    public async Task Handler_failing_twice_then_succeeding_is_retried_with_backoff()
    {
        var order = await PlaceOrderAsync();
        Script.FailTimes(order.Id, 2);

        await DrainAsync();
        var first = await SingleMessageAsync(order.Id);
        Assert.Equal((OutboxStatus.Pending, 1), (first.Status, first.Attempts));
        Assert.Contains("Scripted failure", first.LastError);
        Assert.Equal(_clock.GetUtcNow() + OutboxDispatcher.BackoffAfter(1), first.NextAttemptAt);

        await DrainAsync();
        Assert.Equal(1, (await SingleMessageAsync(order.Id)).Attempts);

        _clock.Advance(OutboxDispatcher.BackoffAfter(1));
        await DrainAsync();
        Assert.Equal(2, (await SingleMessageAsync(order.Id)).Attempts);

        _clock.Advance(OutboxDispatcher.BackoffAfter(2));
        await DrainAsync();

        var done = await SingleMessageAsync(order.Id);
        Assert.Equal((OutboxStatus.Done, 3), (done.Status, done.Attempts));
        Assert.Equal(1, Script.Runs(order.Id));
        Assert.Equal(1, Emails.Count("OrderCreated", order.Id));
    }

    [Fact]
    public async Task Always_failing_message_ends_failed_and_leaves_the_order_untouched()
    {
        var product = await fixture.NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var order = await PlaceOkAsync(customer, Body((product.Sku500, 1)));
        Script.FailTimes(order.Id, int.MaxValue);

        for (var i = 0; i < MaxAttempts + 2; i++)
        {
            await DrainAsync();
            _clock.Advance(TimeSpan.FromMinutes(6));
        }

        var failed = await SingleMessageAsync(order.Id);
        Assert.Equal((OutboxStatus.Failed, MaxAttempts), (failed.Status, failed.Attempts));
        Assert.Null(failed.ProcessedAt);
        Assert.Equal(0, Script.Runs(order.Id));

        var reloaded = await customer.GetAsync($"/orders/{order.Id}");
        Assert.Contains("\"status\":\"Pending\"", await reloaded.Content.ReadAsStringAsync());
        using var scope = fixture.Factory.Services.CreateScope();
        var stock = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Inventories
            .Where(i => i.ProductId == product.ProductId).Select(i => i.StockG).SingleAsync();
        Assert.Equal(20_000 - 500, stock);
    }

    [Fact]
    public async Task Concurrent_dispatchers_claim_each_message_once()
    {
        var product = await fixture.NewProductAsync();
        var customer = await fixture.NewCustomerClientAsync();
        var orders = new List<OrderResponse>();
        for (var i = 0; i < 10; i++)
            orders.Add(await PlaceOkAsync(customer, Body((product.Sku500, 1))));

        await Task.WhenAll(DrainAsync(NewDispatcher(batchSize: 3)), DrainAsync(NewDispatcher(batchSize: 3)));

        foreach (var order in orders)
        {
            var message = await SingleMessageAsync(order.Id);
            Assert.Equal((OutboxStatus.Done, 1), (message.Status, message.Attempts));
            Assert.Equal(1, Script.Runs(order.Id));
        }
    }

    [Fact]
    public async Task Rolled_back_or_replayed_orders_leave_no_outbox_row()
    {
        var plenty = await fixture.NewProductAsync();
        var scarce = await fixture.NewProductAsync(stockG: 500);
        var dollars = await fixture.NewProductAsync(currency: "USD");
        var customer = await fixture.NewCustomerClientAsync();
        var key = Guid.NewGuid().ToString();
        await PlaceOkAsync(customer, Body((plenty.Sku500, 1)), key);
        var before = await OutboxCountAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await PlaceAsync(customer, Body((plenty.Sku500, 1), (scarce.Sku1000, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PlaceAsync(customer, Body((plenty.Sku500, 1), (dollars.Sku500, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PlaceAsync(customer, Body((plenty.Sku500, 1)), key)).StatusCode);

        Assert.Equal(before, await OutboxCountAsync());
    }

    [Fact]
    public async Task Polling_loop_runs_only_when_enabled()
    {
        await using var enabled = fixture.CreateFactory(("Outbox:Enabled", "true"));

        Assert.Single(enabled.Services.GetServices<IHostedService>().OfType<OutboxHostedService>());
        Assert.Empty(fixture.Factory.Services.GetServices<IHostedService>().OfType<OutboxHostedService>());
    }

    [Fact]
    public void Invalid_outbox_settings_fail_at_startup()
    {
        using var factory = fixture.CreateFactory(("Outbox:BatchSize", "0"));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
