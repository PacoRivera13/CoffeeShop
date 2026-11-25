using CoffeeShop.Infrastructure.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace CoffeeShop.Api.Diagnostics;

/// <summary>
/// One readable line per log entry for local development: time, level, short category, the request's correlation id
/// (when the entry was written inside a request) and the message. Other scopes (span, connection, request path) are
/// left out on purpose. Selected with Logging:Console:FormatterName = "correlation".
/// </summary>
public sealed class CorrelationConsoleFormatter([FromKeyedServices(Clocks.Real)] TimeProvider clock) : ConsoleFormatter(Name)
{
    public new const string Name = "correlation";

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
            return;

        string? correlationId = null;
        scopeProvider?.ForEachScope((scope, _) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                foreach (var (key, value) in values)
                    if (key == "CorrelationId")
                        correlationId = value?.ToString();
        }, (object?)null);

        var category = logEntry.Category;
        var shortCategory = category[(category.LastIndexOf('.') + 1)..];

        textWriter.Write(clock.GetLocalNow().ToString("HH:mm:ss")); // Real time, even with the dev clock shifted.
        textWriter.Write(' ');
        textWriter.Write(Level(logEntry.LogLevel));
        textWriter.Write(' ');
        textWriter.Write(shortCategory);
        if (correlationId is not null)
            textWriter.Write($" [{correlationId}]");
        textWriter.Write(' ');
        textWriter.WriteLine(message);
        if (logEntry.Exception is not null)
            textWriter.WriteLine(logEntry.Exception.ToString());
    }

    private static string Level(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "FAIL",
        LogLevel.Critical => "CRIT",
        _ => "    ",
    };
}
