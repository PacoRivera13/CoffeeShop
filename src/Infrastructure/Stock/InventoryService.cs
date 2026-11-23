using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Stock;

internal sealed class InventoryService(CoffeeShopDbContext db, TimeProvider clock) : IInventoryService
{
    private const int MaxAttempts = 2;

    public async Task<IReadOnlyList<InventoryItem>> ListAsync(CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new InventoryItem(
                p.Id,
                p.Name,
                db.Inventories.Where(i => i.ProductId == p.Id).Select(i => i.StockG).FirstOrDefault(),
                db.InventoryMovements.Where(m => m.ProductId == p.Id).Max(m => (DateTimeOffset?)m.CreatedAt)))
            .ToListAsync(ct);

    public async Task<RestockResult> RestockAsync(int productId, int grams, CancellationToken ct)
    {
        if (grams is < 1 or > InventoryConstraints.MaxRestockGrams)
            return new RestockResult.Invalid($"grams must be between 1 and {InventoryConstraints.MaxRestockGrams}.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
                    return (RestockResult)new RestockResult.ProductNotFound();

                var maxBefore = int.MaxValue - grams;
                var updated = await db.Inventories
                    .Where(i => i.ProductId == productId && i.StockG <= maxBefore)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockG, i => i.StockG + grams), ct);

                if (updated == 0)
                {
                    var current = await db.Inventories.Where(i => i.ProductId == productId)
                        .Select(i => (int?)i.StockG).SingleOrDefaultAsync(ct);
                    if (current is not null)
                        return new RestockResult.StockLimitExceeded(current.Value);

                    // First stock for this product: the row does not exist yet.
                    db.Inventories.Add(new Inventory(productId, grams));
                }

                var movement = InventoryMovement.Restock(productId, grams, clock.GetUtcNow());
                db.InventoryMovements.Add(movement);

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (ex.IsUniqueViolation() && attempt < MaxAttempts)
                {
                    // A concurrent restock created the inventory row first; run again as an increment.
                    continue;
                }

                var stock = await db.Inventories.Where(i => i.ProductId == productId).Select(i => i.StockG).SingleAsync(ct);
                await transaction.CommitAsync(ct);
                return new RestockResult.Done(productId, stock, movement);
            }
        });
    }

    public async Task<MovementPage?> GetMovementsAsync(int productId, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
            return null;

        var ledger = db.InventoryMovements.AsNoTracking().Where(m => m.ProductId == productId);
        var totalCount = await ledger.CountAsync(ct);

        // Computed in long: a huge page number would otherwise overflow into a negative OFFSET.
        var skip = (long)(page - 1) * pageSize;
        if (skip >= totalCount)
            return new MovementPage([], page, pageSize, totalCount);

        var items = await ledger
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip((int)skip).Take(pageSize)
            .ToListAsync(ct);

        return new MovementPage(items, page, pageSize, totalCount);
    }
}
