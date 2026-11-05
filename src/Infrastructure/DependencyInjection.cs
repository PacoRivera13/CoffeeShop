using CoffeeShop.Application;
using CoffeeShop.Application.Auth;
using CoffeeShop.Application.Messaging;
using CoffeeShop.Application.Orders;
using CoffeeShop.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using CoffeeShop.Infrastructure.Auth;
using CoffeeShop.Infrastructure.Orders;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        if (section.GetValue(nameof(OutboxOptions.Enabled), defaultValue: true))
            services.AddHostedService<OutboxHostedService>();

        return services;
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
}
