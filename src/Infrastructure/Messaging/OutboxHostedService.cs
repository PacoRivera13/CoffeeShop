using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeShop.Infrastructure.Messaging;

/// <summary>Polls the outbox; registered only when Outbox:Enabled is true, so a separate worker host can own it.</summary>
public sealed class OutboxHostedService(
    OutboxDispatcher dispatcher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                claimed = await dispatcher.DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch failed; retrying after the poll interval.");
            }

            // A full batch means more work is likely waiting, so poll again at once.
            if (claimed >= settings.BatchSize)
                continue;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(settings.PollIntervalMs), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
