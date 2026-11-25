using CoffeeShop.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoffeeShop.Api;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("/health").WithTags("Health").AllowAnonymous();

        health.MapGet("", (HealthCheckService checks, CancellationToken ct) => ReportAsync(checks, _ => true, ct))
            .WithName("GetHealth")
            .WithSummary("Liveness plus readiness of SQL Server and the storage bucket; 503 when any check fails. No configuration is exposed.")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        health.MapGet("/live", (HealthCheckService checks, CancellationToken ct) =>
                ReportAsync(checks, c => c.Tags.Contains(HealthChecks.Live), ct))
            .WithName("GetLiveness")
            .WithSummary("Liveness only: the process is up and answering.")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> ReportAsync(
        HealthCheckService checks, Func<HealthCheckRegistration, bool> predicate, CancellationToken ct)
    {
        var report = await checks.CheckHealthAsync(predicate, ct);
        var body = new HealthResponse(
            report.Status.ToString(),
            report.Entries.Select(e => new HealthCheckResponse(e.Key, e.Value.Status.ToString(), (int)e.Value.Duration.TotalMilliseconds)).ToList());

        return Results.Json(body, statusCode: report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK);
    }
}

public sealed record HealthResponse(string Status, IReadOnlyList<HealthCheckResponse> Checks);

public sealed record HealthCheckResponse(string Name, string Status, int DurationMs);
