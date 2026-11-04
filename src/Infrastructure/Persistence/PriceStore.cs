using CoffeeShop.Application;
using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Persistence;

internal sealed class PriceStore(CoffeeShopDbContext db) : IPriceStore
{
    public async Task<bool> TryAddAsync(EffectivePrice price, CancellationToken ct)
    {
        db.EffectivePrices.Add(price);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<EffectivePrice>> GetHistoryAsync(int skuId, CancellationToken ct) =>
        await db.EffectivePrices.AsNoTracking()
            .Where(p => p.SkuId == skuId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(ct);
}
