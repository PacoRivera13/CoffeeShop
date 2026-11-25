using CoffeeShop.Application;
using CoffeeShop.Application.Auth;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Orders;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Orders;
using CoffeeShop.Infrastructure.Persistence;
using CoffeeShop.Infrastructure.Stock;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoffeeShop.Infrastructure.Seeding;

/// <summary>
/// Opt-in demo data for local development (Seed:Demo=true). Everything goes through the domain entities and the
/// application services, so stock matches the movement ledger and every order has its outbox messages.
/// </summary>
public static class DemoSeeder
{
    public const string FlagKey = "Seed:Demo";
    public const string MarkerProduct = "Chiapas Altura";

    private sealed record DemoProduct(string Name, string Origin, string Roast, decimal Price500, decimal Price1000, int StockG);

    private static readonly DemoProduct[] Catalogue =
    [
        new(MarkerProduct, "Mexico, Chiapas", "Medium", 189m, 349m, 15_000),
        new("Veracruz Pluma", "Mexico, Veracruz", "Medium-Dark", 179m, 329m, 12_000),
        new("Oaxaca Pluma Hidalgo", "Mexico, Oaxaca", "Medium", 219m, 409m, 8_000),
        new("Huehuetenango", "Guatemala", "Medium", 209m, 389m, 6_000),
        new("Yirgacheffe", "Ethiopia", "Light", 259m, 489m, 1_500),
        new("Cerrado", "Brazil", "Dark", 169m, 309m, 400),
    ];

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(FlagKey);

    /// <summary>Throws when the demo seed is requested outside Development.</summary>
    public static void EnsureAllowed(IConfiguration configuration, IHostEnvironment environment)
    {
        if (IsEnabled(configuration) && !environment.IsDevelopment())
            throw new InvalidOperationException(
                $"Seed__Demo=true is only allowed when ASPNETCORE_ENVIRONMENT=Development (current: {environment.EnvironmentName}). " +
                "Remove Seed__Demo to start in this environment.");
    }

    /// <summary>Seeds the demo catalogue, stock, customer and orders once; skips when the marker product exists.</summary>
    public static async Task SeedDemoAsync(
        this IServiceProvider services, IConfiguration configuration, IHostEnvironment environment, CancellationToken ct = default)
    {
        EnsureAllowed(configuration, environment);
        if (!IsEnabled(configuration))
            return;

        var email = configuration["Seed:CustomerEmail"];
        var password = configuration["Seed:CustomerPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Seed__Demo=true needs Seed__CustomerEmail and Seed__CustomerPassword.");

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DemoSeeder));
        var db = sp.GetRequiredService<CoffeeShopDbContext>();

        if (await db.Products.AnyAsync(p => p.Name == MarkerProduct, ct))
        {
            logger.LogInformation("Demo data already present; skipping the demo seed.");
            return;
        }

        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var currency = sp.GetRequiredService<PricingSettings>().Currency;

        var (productIds, skus) = await SeedCatalogueAsync(db, currency, today, ct);
        await SeedStockAsync(sp, productIds, today.AddDays(-90), ct);
        var userId = await EnsureCustomerAsync(sp, email, password, ct);
        await SeedOrdersAsync(sp, db, userId, skus, now, ct);

        logger.LogInformation("Demo data seeded: {Products} products and 4 orders for the demo customer.", Catalogue.Length);
    }

    private static async Task<(Dictionary<string, int> ProductIds, Dictionary<(string, Grind, int), int> SkuIds)> SeedCatalogueAsync(
        CoffeeShopDbContext db, string currency, DateTimeOffset today, CancellationToken ct)
    {
        var current = today.AddDays(-90);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var products = Catalogue.Select(d => (Demo: d, Product: new Product(0, d.Name, d.Origin, d.Roast))).ToList();
        db.Products.AddRange(products.Select(p => p.Product));
        await db.SaveChangesAsync(ct);

        var skus = new Dictionary<(string, Grind, int), Sku>();
        foreach (var (demo, product) in products)
            foreach (var grind in new[] { Grind.WholeBean, Grind.Ground })
                foreach (var size in new[] { 500, 1000 })
                    skus[(demo.Name, grind, size)] = new Sku(0, product.Id, grind, size);
        db.Skus.AddRange(skus.Values);
        await db.SaveChangesAsync(ct);

        foreach (var ((name, _, size), sku) in skus)
        {
            var demo = Catalogue.Single(d => d.Name == name);
            var amount = size == 500 ? demo.Price500 : demo.Price1000;
            db.EffectivePrices.Add(new EffectivePrice(0, sku.Id, amount, currency, current));

            // History: Chiapas was cheaper a year ago. Future pricing: Yirgacheffe goes up 10% in a week.
            if (name == MarkerProduct)
                db.EffectivePrices.Add(new EffectivePrice(0, sku.Id, size == 500 ? 169m : 315m, currency, today.AddDays(-400)));
            if (name == "Yirgacheffe")
                db.EffectivePrices.Add(new EffectivePrice(0, sku.Id, decimal.Round(amount * 1.10m, 2), currency, today.AddDays(7)));
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return (products.ToDictionary(p => p.Demo.Name, p => p.Product.Id), skus.ToDictionary(kv => kv.Key, kv => kv.Value.Id));
    }

    private static async Task SeedStockAsync(
        IServiceProvider sp, IReadOnlyDictionary<string, int> productIds, DateTimeOffset at, CancellationToken ct)
    {
        var inventory = ActivatorUtilities.CreateInstance<InventoryService>(sp, new FixedTimeProvider(at));
        foreach (var demo in Catalogue)
        {
            var result = await inventory.RestockAsync(productIds[demo.Name], demo.StockG, ct);
            if (result is not RestockResult.Done)
                throw new InvalidOperationException($"Demo restock of {demo.Name} failed: {result}.");
        }
    }

    private static async Task<string> EnsureCustomerAsync(IServiceProvider sp, string email, string password, CancellationToken ct)
    {
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.FindByEmailAsync(email) is null)
        {
            var result = await sp.GetRequiredService<IAuthService>()
                .RegisterAsync(new RegisterCommand(email, password, "Demo Customer"), ct);
            if (result is not RegisterResult.Success)
                throw new InvalidOperationException($"Could not register the demo customer: {result}.");
        }

        return (await users.FindByEmailAsync(email))!.Id;
    }

    private static async Task SeedOrdersAsync(
        IServiceProvider sp, CoffeeShopDbContext db, string userId,
        IReadOnlyDictionary<(string, Grind, int), int> skus, DateTimeOffset now, CancellationToken ct)
    {
        int Sku(string product, Grind grind, int size) => skus[(product, grind, size)];

        await PlaceAsync("demo-pending", now, [new(Sku(MarkerProduct, Grind.WholeBean, 500), 2)]);

        var paid = await PlaceAsync("demo-paid", now.AddDays(-3), [new(Sku("Veracruz Pluma", Grind.Ground, 1000), 1)]);
        await ActAsync(now.AddDays(-3), paid, (orders, id) => orders.PayAsync(userId, id, ct));

        var delivered = await PlaceAsync("demo-delivered", now.AddDays(-21),
        [
            new(Sku("Oaxaca Pluma Hidalgo", Grind.WholeBean, 500), 1),
            new(Sku(MarkerProduct, Grind.Ground, 500), 1),
        ]);
        await ActAsync(now.AddDays(-21), delivered, (orders, id) => orders.PayAsync(userId, id, ct));
        db.ChangeTracker.Clear();
        var order = await db.Orders.SingleAsync(o => o.Id == delivered, ct);
        order.StartPreparing();
        order.Ship();
        order.Deliver();
        await db.SaveChangesAsync(ct);

        var cancelled = await PlaceAsync("demo-cancelled", now.AddDays(-10), [new(Sku("Veracruz Pluma", Grind.WholeBean, 500), 1)]);
        await ActAsync(now.AddDays(-10), cancelled, (orders, id) => orders.CancelAsync(userId, id, ct));

        async Task<int> PlaceAsync(string key, DateTimeOffset at, IReadOnlyList<OrderItem> items)
        {
            var result = await OrdersAt(at).CreateAsync(userId, items, key, ct);
            return result switch
            {
                CreateOrderResult.Created created => created.Order.Id,
                CreateOrderResult.Replayed replayed => replayed.Order.Id,
                _ => throw new InvalidOperationException($"Demo order '{key}' failed: {result}."),
            };
        }

        async Task ActAsync(DateTimeOffset at, int orderId, Func<IOrderService, int, Task<OrderActionResult>> action)
        {
            var result = await action(OrdersAt(at), orderId);
            if (result is not OrderActionResult.Done)
                throw new InvalidOperationException($"Demo action on order {orderId} failed: {result}.");
        }

        IOrderService OrdersAt(DateTimeOffset at) =>
            ActivatorUtilities.CreateInstance<OrderService>(sp, new FixedTimeProvider(at));
    }

    /// <summary>Backdates the services the seeder drives, so orders, movements and outbox rows share one timeline.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
