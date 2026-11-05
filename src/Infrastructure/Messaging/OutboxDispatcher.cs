using CoffeeShop.Application.Messaging;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeShop.Infrastructure.Messaging;

public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    public bool Enabled { get; set; } = true;
    public int PollIntervalMs { get; set; } = 1000;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
}

/// <summary>Claims due outbox messages and publishes them, at least once, with retries and exponential backoff.</summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger)
{
    /// <summary>How long a claimed message stays hidden from other dispatchers; a crashed dispatcher's claims reappear after it.</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    /// <summary>Delays before the next attempt: 2s, 4s, 8s... capped at 5 minutes.</summary>
    public static TimeSpan BackoffAfter(int attempts) =>
        TimeSpan.FromTicks(Math.Min(BaseBackoff.Ticks * (1L << Math.Min(attempts - 1, 20)), MaxBackoff.Ticks));

    /// <summary>Processes one batch of due messages and returns how many were claimed.</summary>
    public async Task<int> DispatchOnceAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var claimed = await ClaimAsync(settings.BatchSize, ct);

        foreach (var message in claimed)
        {
            try
            {
                await DeliverAsync(message, settings.MaxAttempts, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Recording the outcome failed; the message reappears when its lease expires. Keep the rest of the batch going.
                logger.LogError(ex, "Could not record the outcome of outbox message {MessageId}", message.Id);
            }
        }

        return claimed.Count;
    }

    /// <summary>
    /// One short statement: UPDLOCK takes the rows, READPAST skips rows another dispatcher holds, and moving
    /// NextAttemptAt out by the lease keeps them invisible after this statement commits.
    /// </summary>
    private async Task<List<OutboxMessage>> ClaimAsync(int batchSize, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var now = clock.GetUtcNow();
        var leaseUntil = now + ClaimLease;

        return await db.OutboxMessages
            .FromSqlInterpolated($"""
                WITH due AS (
                    SELECT TOP ({batchSize}) *
                    FROM [OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE [Status] = 'Pending' AND [NextAttemptAt] <= {now}
                    ORDER BY [NextAttemptAt])
                UPDATE due SET [NextAttemptAt] = {leaseUntil}
                OUTPUT inserted.*;
                """)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private async Task DeliverAsync(OutboxMessage message, int maxAttempts, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var attempts = message.Attempts + 1;

        try
        {
            await bus.PublishAsync(message.ToEnvelope(), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            var status = attempts >= maxAttempts ? OutboxStatus.Failed : OutboxStatus.Pending;
            var error = $"{ex.GetType().Name}: {ex.Message}";
            if (error.Length > OutboxMessage.MaxErrorLength)
                error = error[..OutboxMessage.MaxErrorLength];

            logger.LogWarning(ex, "Outbox message {MessageId} ({MessageType}) failed attempt {Attempt} of {MaxAttempts}",
                message.Id, message.Type, attempts, maxAttempts);

            var failedRows = await StillClaimed(db, message).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Attempts, attempts)
                .SetProperty(m => m.Status, status)
                .SetProperty(m => m.NextAttemptAt, now + BackoffAfter(attempts))
                .SetProperty(m => m.LastError, error), ct);
            WarnIfClaimLost(failedRows, message);
            return;
        }

        var processedAt = clock.GetUtcNow();
        var doneRows = await StillClaimed(db, message).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Attempts, attempts)
            .SetProperty(m => m.Status, OutboxStatus.Done)
            .SetProperty(m => m.ProcessedAt, processedAt), ct);
        WarnIfClaimLost(doneRows, message);
    }

    /// <summary>
    /// Matches the row only while it still carries this claim's lease. If the lease expired and another dispatcher
    /// reclaimed the message, that dispatcher owns the outcome and this stale one must not overwrite it.
    /// </summary>
    private static IQueryable<OutboxMessage> StillClaimed(CoffeeShopDbContext db, OutboxMessage claimed) =>
        db.OutboxMessages.Where(m =>
            m.Id == claimed.Id && m.Status == OutboxStatus.Pending && m.NextAttemptAt == claimed.NextAttemptAt);

    private void WarnIfClaimLost(int updatedRows, OutboxMessage message)
    {
        if (updatedRows == 0)
            logger.LogWarning("Outbox message {MessageId} was reclaimed after its lease expired; its outcome was left to the new claim",
                message.Id);
    }
}
