using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Tests;

public class CoffeeShopDbContextTests
{
    private static CoffeeShopDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new CoffeeShopDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void Seed_has_three_products_with_four_priced_skus_each()
    {
        using var db = CreateContext();

        Assert.Equal(3, db.Products.Count());
        Assert.All(db.Skus.AsEnumerable().GroupBy(s => s.ProductId), g => Assert.Equal(4, g.Count()));
        Assert.Equal(12, db.Skus.Count());
        Assert.Equal(12, db.EffectivePrices.Select(p => p.SkuId).Distinct().Count());
    }

    [Fact]
    public void Effective_price_is_unique_per_sku_and_date()
    {
        using var db = CreateContext();

        var index = db.Model.FindEntityType(typeof(EffectivePrice))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(EffectivePrice.SkuId), nameof(EffectivePrice.EffectiveFrom)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task Inserting_a_price_is_allowed()
    {
        using var db = CreateContext();
        db.EffectivePrices.Add(new EffectivePrice(100, 2, 25m, "EUR", new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(1, await db.SaveChangesAsync());
    }

    [Fact]
    public async Task Updating_a_price_is_rejected()
    {
        using var db = CreateContext();
        var price = await db.EffectivePrices.FirstAsync();
        db.Entry(price).Property(p => p.Amount).CurrentValue = 99m;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task Deleting_a_price_is_rejected()
    {
        using var db = CreateContext();
        db.EffectivePrices.Remove(await db.EffectivePrices.FirstAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
