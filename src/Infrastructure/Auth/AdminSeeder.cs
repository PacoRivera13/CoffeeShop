using CoffeeShop.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoffeeShop.Infrastructure.Auth;

public static class AdminSeeder
{
    /// <summary>Creates the Admin user from Admin:Email and Admin:Password. An existing user keeps its password.</summary>
    public static async Task SeedAdminAsync(this IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        var hasEmail = !string.IsNullOrWhiteSpace(email);
        var hasPassword = !string.IsNullOrWhiteSpace(password);

        if (hasEmail != hasPassword)
            services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AdminSeeder))
                .LogWarning("Admin user not seeded: Admin:Email and Admin:Password must both be set.");
        if (!hasEmail || !hasPassword)
            return;

        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = await users.FindByEmailAsync(email!);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email };
            EnsureSucceeded(await users.CreateAsync(user, password!));
        }

        if (!await users.IsInRoleAsync(user, AppRoles.Admin))
            EnsureSucceeded(await users.AddToRoleAsync(user, AppRoles.Admin));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Could not seed the Admin user: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
    }
}
