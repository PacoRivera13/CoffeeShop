using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CoffeeShop.Api.Diagnostics;

/// <summary>
/// Gives every request a correlation id (the caller's X-Correlation-Id when it is safe, otherwise a new one), echoes it
/// on the response, adds it to the scope of every log line written while handling the request, and writes one
/// summary line per request.
/// </summary>
public static partial class RequestDiagnostics
{
    public const string HeaderName = "X-Correlation-Id";
    private const string LoggerCategory = "CoffeeShop.Requests";

    public static IApplicationBuilder UseRequestDiagnostics(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

        return app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers[HeaderName] is [var given] && SafeId().IsMatch(given!)
                ? given!
                : Guid.NewGuid().ToString("N");
            context.TraceIdentifier = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            // A template scope prints as "CorrelationId:abc" in the console and stays a structured property elsewhere.
            using var scope = logger.BeginScope("CorrelationId:{CorrelationId}", correlationId);
            var started = Stopwatch.GetTimestamp();
            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs:0} ms",
                    context.Request.Method, context.Request.Path.Value, context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        });
    }

    /// <summary>Only short, plain ids are echoed, so a header cannot inject text into logs or responses.</summary>
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeId();
}
