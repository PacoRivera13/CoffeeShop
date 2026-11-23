using CoffeeShop.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoffeeShop.Api.Tests;

public static class TestDispatcher
{
    public const int MaxAttempts = 5;

    /// <summary>A dispatcher on the shared factory driven by the given clock, so tests control retries and backoff.</summary>
    public static OutboxDispatcher Create(MsSqlFixture fixture, TimeProvider clock, int batchSize = 100) => new(
        fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
        clock,
        Options.Create(new OutboxOptions { BatchSize = batchSize, MaxAttempts = MaxAttempts }),
        NullLogger<OutboxDispatcher>.Instance);

    /// <summary>Dispatches until nothing is due; the shared database may also hold other tests' messages.</summary>
    public static async Task<int> DrainAsync(this OutboxDispatcher dispatcher)
    {
        var total = 0;
        for (int claimed; (claimed = await dispatcher.DispatchOnceAsync(CancellationToken.None)) > 0;)
            total += claimed;
        return total;
    }
}
