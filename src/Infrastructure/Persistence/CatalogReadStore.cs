using CoffeeShop.Application;
using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Persistence;

internal sealed class CatalogReadStore(CoffeeShopDbContext db) : ICatalogReadStore
{
    public async Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Include(p => p.Skus.OrderBy(s => s.Id))
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

    public Task<bool> SkuExistsAsync(int skuId, CancellationToken ct) =>
        db.Skus.AnyAsync(s => s.Id == skuId, ct);

    public async Task<IReadOnlyList<EffectivePrice>> GetPricesAsync(int skuId, CancellationToken ct) =>
        await db.EffectivePrices.AsNoTracking()
            .Where(p => p.SkuId == skuId)
            .ToListAsync(ct);
}
