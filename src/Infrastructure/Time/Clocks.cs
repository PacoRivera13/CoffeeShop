using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoffeeShop.Infrastructure.Time;

public static class Clocks
{
    /// <summary>
    /// Keyed TimeProvider that always follows the real time. Token lifetimes and presigned URLs are checked by parties
    /// that use the real clock (JwtBearer, S3), so they must never see the shifted development clock.
    /// </summary>
    public const string Real = "real-clock";

    public const string OffsetKey = "Dev:ClockOffsetDays";
    private const int MaxOffsetDays = 3650;

    /// <summary>
    /// Registers the application clock and the real clock. Dev:ClockOffsetDays shifts the application clock in
    /// Development only; set in any other environment it fails startup. Returns the active offset in days, or 0.
    /// </summary>
    public static int AddClocks(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var offsetDays = ReadOffsetDays(configuration, environment);
        services.AddSingleton(offsetDays == 0 ? TimeProvider.System : new OffsetTimeProvider(TimeProvider.System, TimeSpan.FromDays(offsetDays)));
        services.AddKeyedSingleton(Real, TimeProvider.System);
        return offsetDays;
    }

    private static int ReadOffsetDays(IConfiguration configuration, IHostEnvironment environment)
    {
        var raw = configuration[OffsetKey];
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        if (!environment.IsDevelopment())
            throw new InvalidOperationException(
                $"Dev__ClockOffsetDays is only allowed when ASPNETCORE_ENVIRONMENT=Development (current: {environment.EnvironmentName}). " +
                "Remove it to start in this environment.");

        if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var days)
            || Math.Abs(days) > MaxOffsetDays)
            throw new InvalidOperationException($"Dev__ClockOffsetDays must be a whole number of days between -{MaxOffsetDays} and {MaxOffsetDays}.");

        return days;
    }
}

/// <summary>The system clock shifted by a fixed offset; timers and timestamps for elapsed time are not shifted.</summary>
public sealed class OffsetTimeProvider(TimeProvider inner, TimeSpan offset) : TimeProvider
{
    public TimeSpan Offset { get; } = offset;

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + Offset;

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;
}
