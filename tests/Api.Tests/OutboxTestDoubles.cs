using System.Collections.Concurrent;
using CoffeeShop.Application.Messaging;
using CoffeeShop.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoffeeShop.Api.Tests;

public static class OutboxTestDoubles
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<CapturedNotifications>();
        services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<CapturedNotifications>());
        services.AddSingleton<HandlerScript>();
        services.AddScoped<IMessageHandler<OrderCreated>, ScriptedHandler>();
    }
}

/// <summary>A clock tests move by hand, so retries and backoff need no sleeps.</summary>
public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Keeps the log lines written by NotificationHandler, the stand-in for sending emails.</summary>
public sealed class CapturedNotifications : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public int Count(string messageType, int orderId) =>
        _lines.Count(l => l.StartsWith($"Email sent for {messageType} of order {orderId} ", StringComparison.Ordinal));

    public ILogger CreateLogger(string categoryName) =>
        categoryName == typeof(NotificationHandler).FullName ? new Logger(_lines) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public void Dispose() { }

    private sealed class Logger(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue(formatter(state, exception));
    }
}

/// <summary>Lets a test make the scripted handler fail a number of times for one order.</summary>
public sealed class HandlerScript
{
    private readonly ConcurrentDictionary<int, int> _failuresLeft = new();
    private readonly ConcurrentDictionary<int, int> _runs = new();

    public void FailTimes(int orderId, int times) => _failuresLeft[orderId] = times;

    public int Runs(int orderId) => _runs.GetValueOrDefault(orderId);

    public void Run(int orderId)
    {
        if (_failuresLeft.TryGetValue(orderId, out var left) && left > 0)
        {
            _failuresLeft[orderId] = left - 1;
            throw new InvalidOperationException($"Scripted failure for order {orderId}.");
        }

        _runs.AddOrUpdate(orderId, 1, (_, runs) => runs + 1);
    }
}

public sealed class ScriptedHandler(HandlerScript script) : IMessageHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
    {
        script.Run(message.OrderId);
        return Task.CompletedTask;
    }
}
