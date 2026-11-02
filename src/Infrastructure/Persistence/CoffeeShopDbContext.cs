using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Persistence;

public class CoffeeShopDbContext(DbContextOptions<CoffeeShopDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sku> Skus => Set<Sku>();
    public DbSet<EffectivePrice> EffectivePrices => Set<EffectivePrice>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Inventory> Inventories => Set<Inventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoffeeShopDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureEffectivePricesAreInsertOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureEffectivePricesAreInsertOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureEffectivePricesAreInsertOnly()
    {
        var changed = ChangeTracker.Entries<EffectivePrice>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (changed)
            throw new InvalidOperationException("EffectivePrice rows are insert-only.");
    }
}
