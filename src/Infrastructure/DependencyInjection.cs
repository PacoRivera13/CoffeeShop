using CoffeeShop.Application;
using CoffeeShop.Application.Auth;
using CoffeeShop.Infrastructure.Auth;
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
