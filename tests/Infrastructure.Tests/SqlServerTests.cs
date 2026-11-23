using CoffeeShop.Application;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Pricing;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Infrastructure.Tests;

/// <summary>Runs on LocalDB on Windows; elsewhere set COFFEESHOP_TEST_SQLSERVER or the test is reported as skipped.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (SqlServerFixture.ServerConnectionString is null)
            Skip = "Set COFFEESHOP_TEST_SQLSERVER to run SQL Server tests on this platform.";
    }
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    public static string? ServerConnectionString { get; } =
        Environment.GetEnvironmentVariable("COFFEESHOP_TEST_SQLSERVER")
        ?? (OperatingSystem.IsWindows() ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True" : null);

    private ServiceProvider? _services;

    public IServiceScope CreateScope() => _services!.CreateScope();

    public async Task InitializeAsync()
    {
        if (ServerConnectionString is null)
            return;

        var connectionString = new SqlConnectionStringBuilder(ServerConnectionString)
        {
            InitialCatalog = $"CoffeeShop_Test_{Guid.NewGuid():N}"
        }.ConnectionString;

        _services = new ServiceCollection().AddInfrastructure(connectionString).BuildServiceProvider();

        using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is null)
            return;

        using (var scope = CreateScope())
            await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Database.EnsureDeletedAsync();

        await _services.DisposeAsync();
    }
}

public class SqlServerTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset Jun = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [SqlServerFact]
    public async Task Migration_applies_schema_and_seed()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(3, await db.Products.CountAsync());
        Assert.Equal(12, await db.Skus.CountAsync());
        Assert.Equal(14, await db.EffectivePrices.CountAsync(p => p.Currency == "EUR"));
        Assert.Equal(13, await db.EffectivePrices.CountAsync(p => p.Currency == "MXN"));
    }

    [SqlServerFact]
    public async Task Seeded_catalog_is_priced_in_mxn_from_the_migration_on()
    {
        using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICatalogReadStore>();
        var now = DateTimeOffset.UtcNow;
        var afterScheduledChange = new DateTimeOffset(2027, 1, 2, 0, 0, 0, TimeSpan.Zero);

        var current = PriceResolver.Resolve(await store.GetPricesAsync(1, now, CancellationToken.None), now);
        var scheduled = PriceResolver.Resolve(
            await store.GetPricesAsync(1, afterScheduledChange, CancellationToken.None), afterScheduledChange);
        var history = PriceResolver.Resolve(await store.GetPricesAsync(1, Jun, CancellationToken.None), Jun);

        // 14 EUR and 15 EUR at 1 EUR = 20 MXN; the EUR history before the migration is untouched.
        Assert.Equal(("MXN", 280.00m), (current.Currency, current.Amount));
        Assert.Equal(("MXN", 300.00m), (scheduled.Currency, scheduled.Amount));
        Assert.Equal(("EUR", 14.00m), (history.Currency, history.Amount));
    }

    [SqlServerFact]
    public async Task Store_returns_products_with_their_skus()
    {
        using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICatalogReadStore>();

        var products = await store.GetProductsAsync(CancellationToken.None);

        Assert.Equal([1, 2, 3], products.Select(p => p.Id));
        Assert.All(products, p => Assert.Equal(4, p.Skus.Count));
    }

    [SqlServerFact]
    public async Task Store_returns_only_prices_of_the_sku_up_to_as_of()
    {
        using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICatalogReadStore>();

        var prices = await store.GetPricesAsync(1, Jun, CancellationToken.None);

        Assert.Equal([12.00m, 14.00m], prices.Select(p => p.Amount).Order());
        Assert.Empty(await store.GetPricesAsync(1, Jun.AddYears(-1), CancellationToken.None));
    }

    [SqlServerFact]
    public async Task Store_reports_whether_a_sku_exists()
    {
        using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICatalogReadStore>();

        Assert.True(await store.SkuExistsAsync(1, CancellationToken.None));
        Assert.False(await store.SkuExistsAsync(9999, CancellationToken.None));
    }

    [SqlServerFact]
    public async Task New_price_can_be_inserted_through_ef()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        db.EffectivePrices.Add(new EffectivePrice(0, 2, 25m, "MXN", Jun));

        Assert.Equal(1, await db.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Bulk_update_of_prices_is_rejected_by_the_trigger()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        await Assert.ThrowsAsync<SqlException>(() =>
            db.EffectivePrices.ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, 99m)));
    }

    [SqlServerFact]
    public async Task Bulk_delete_of_prices_is_rejected_by_the_trigger()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        await Assert.ThrowsAsync<SqlException>(() => db.EffectivePrices.ExecuteDeleteAsync());
    }

    [SqlServerFact]
    public async Task Duplicate_sku_and_effective_date_is_rejected()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        db.EffectivePrices.Add(new EffectivePrice(0, 1, 1m, "MXN", Jun));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Zero_amount_is_rejected_by_the_check_constraint()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO EffectivePrices (SkuId, Amount, Currency, EffectiveFrom) VALUES (1, 0, 'MXN', '2030-01-01T00:00:00+00:00')"));
    }

    [SqlServerFact]
    public async Task Sku_size_outside_the_allowed_values_is_rejected_by_the_check_constraint()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Skus (ProductId, Grind, SizeG) VALUES (1, 'Ground', 250)"));
    }

    [SqlServerFact]
    public async Task Order_line_must_reference_a_price_of_its_own_sku()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = new IdentityUser { UserName = "test@example.test", Email = "test@example.test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var customer = new Customer(0, "Test", "test@example.test", user.Id);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var order = Order.Place(customer.Id, [(await db.EffectivePrices.SingleAsync(p => p.Id == 1), 1)], null, Jun);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.OrderLines.Add(new OrderLine(0, order.Id, skuId: 1, qty: 1, unitPriceSnapshot: 12m, priceId: 1));
        Assert.Equal(1, await db.SaveChangesAsync());

        db.OrderLines.Add(new OrderLine(0, order.Id, skuId: 2, qty: 1, unitPriceSnapshot: 12m, priceId: 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
