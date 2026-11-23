using CoffeeShop.Application;
using CoffeeShop.Application.Auth;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Messaging;
using CoffeeShop.Application.Orders;
using CoffeeShop.Application.Storage;
using CoffeeShop.Infrastructure.Auth;
using CoffeeShop.Infrastructure.Invoicing;
using CoffeeShop.Infrastructure.Messaging;
using CoffeeShop.Infrastructure.Orders;
using CoffeeShop.Infrastructure.Persistence;
using CoffeeShop.Infrastructure.Stock;
using CoffeeShop.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoffeeShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<CoffeeShopDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ICatalogReadStore, CatalogReadStore>();
        services.AddScoped<IPriceStore, PriceStore>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInventoryService, InventoryService>();
        return services;
    }

    /// <summary>Registers the outbox dispatcher, the in-process bus and handlers; the polling loop only when Outbox:Enabled.</summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(OutboxOptions.Section);
        services.AddOptions<OutboxOptions>()
            .Bind(section)
            .Validate(o => o.PollIntervalMs > 0 && o.BatchSize > 0 && o.MaxAttempts > 0,
                "Outbox:PollIntervalMs, Outbox:BatchSize and Outbox:MaxAttempts must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IMessageBus, InProcessMessageBus>();
        services.AddSingleton<OutboxDispatcher>();
        services.AddScoped<IMessageHandler<OrderCreated>, NotificationHandler>();
        services.AddScoped<IMessageHandler<OrderPaid>, NotificationHandler>();
        services.AddScoped<IMessageHandler<OrderCancelled>, NotificationHandler>();
        services.AddSingleton<InvoicePdfRenderer>();
        services.AddScoped<IMessageHandler<OrderPaid>, InvoiceHandler>();

        if ((section.Get<OutboxOptions>() ?? new OutboxOptions()).Enabled)
            services.AddHostedService<OutboxHostedService>();

        return services;
    }

    /// <summary>Registers the S3-compatible file storage; its settings are validated at startup.</summary>
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.Section))
            .Validate(o => IsAbsoluteUrl(o.Endpoint), "Storage:Endpoint must be an absolute http(s) URL.")
            .Validate(o => string.IsNullOrWhiteSpace(o.PublicEndpoint) || IsAbsoluteUrl(o.PublicEndpoint),
                "Storage:PublicEndpoint must be empty or an absolute http(s) URL.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Bucket) && !string.IsNullOrWhiteSpace(o.AccessKey)
                    && !string.IsNullOrWhiteSpace(o.SecretKey) && !string.IsNullOrWhiteSpace(o.Region),
                "Storage:Bucket, Storage:AccessKey, Storage:SecretKey and Storage:Region must be configured.")
            .ValidateOnStart();

        services.AddSingleton<S3FileStorage>();
        services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<S3FileStorage>());
        return services;
    }

    /// <summary>Creates the storage bucket at startup when Storage:CreateBucketIfMissing is set (development).</summary>
    public static async Task EnsureStorageBucketAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        if (services.GetRequiredService<IOptions<StorageOptions>>().Value.CreateBucketIfMissing)
            await services.GetRequiredService<S3FileStorage>().EnsureBucketAsync(ct);
    }

    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, JwtOptions jwtOptions)
    {
        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CoffeeShopDbContext>();

        services.AddSingleton(jwtOptions);
        services.AddSingleton<JwtTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }

    private static bool IsAbsoluteUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
