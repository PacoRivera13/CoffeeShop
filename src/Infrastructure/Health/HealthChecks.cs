using CoffeeShop.Infrastructure.Persistence;
using CoffeeShop.Application.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoffeeShop.Infrastructure.Health;

public static class HealthChecks
{
    public const string Live = "live";
    public const string Ready = "ready";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>Liveness (the process answers) and readiness (SQL Server and the storage bucket are reachable).</summary>
    public static IServiceCollection AddCoffeeShopHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [Live])
            .AddCheck<SqlServerHealthCheck>("sql", tags: [Ready], timeout: Timeout)
            .AddCheck<StorageHealthCheck>("storage", tags: [Ready], timeout: Timeout);
        return services;
    }
}

/// <summary>Failures carry no exception or message, so nothing about the server leaks into a health response.</summary>
internal sealed class SqlServerHealthCheck(CoffeeShopDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}

internal sealed class StorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await storage.IsReadyAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
