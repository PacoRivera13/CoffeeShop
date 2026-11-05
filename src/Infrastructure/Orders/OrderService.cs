using CoffeeShop.Application.Messaging;
using CoffeeShop.Application.Orders;
using CoffeeShop.Infrastructure.Messaging;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Pricing;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Orders;

internal sealed class OrderService(CoffeeShopDbContext db, TimeProvider clock) : IOrderService
{
    private const int MaxConcurrencyAttempts = 3;

    public async Task<CreateOrderResult> CreateAsync(
        string userId, IReadOnlyList<OrderItem> items, string? idempotencyKey, CancellationToken ct)
    {
        if (idempotencyKey is not null
            && (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > OrderConstraints.MaxIdempotencyKeyLength))
            return new CreateOrderResult.Invalid(
                $"Idempotency-Key must have between 1 and {OrderConstraints.MaxIdempotencyKeyLength} characters.");

        if (await FindCustomerIdAsync(userId, ct) is not { } customerId)
            return new CreateOrderResult.CustomerNotFound();

        // A retry returns the original order even if its body would no longer validate.
        if (idempotencyKey is not null && await FindByKeyAsync(customerId, idempotencyKey, ct) is { } existing)
            return new CreateOrderResult.Replayed(existing);

        if (Validate(items) is { } error)
            return new CreateOrderResult.Invalid(error);

        var merged = items.GroupBy(i => i.SkuId)
            .Select(g => new OrderItem(g.Key, g.Sum(i => i.Qty)))
            .OrderBy(i => i.SkuId)
            .ToList();
        if (merged.FirstOrDefault(i => i.Qty > OrderConstraints.MaxQty) is { } tooMany)
            return new CreateOrderResult.Invalid(
                $"SKU {tooMany.SkuId} adds up to {tooMany.Qty} units; the maximum per SKU is {OrderConstraints.MaxQty}.");

        var skuIds = merged.Select(i => i.SkuId).ToList();
        var skus = await db.Skus.AsNoTracking().Where(s => skuIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var missing = skuIds.Where(id => !skus.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            return new CreateOrderResult.SkusNotFound(missing);

        var now = clock.GetUtcNow();
        var candidates = await db.EffectivePrices.AsNoTracking()
            .Where(p => skuIds.Contains(p.SkuId) && p.EffectiveFrom <= now)
            .ToListAsync(ct);
        var unpriced = skuIds.Where(id => candidates.All(p => p.SkuId != id)).ToList();
        if (unpriced.Count > 0)
            return new CreateOrderResult.NoEffectivePrice(unpriced);

        var lines = merged
            .Select(i => (PriceResolver.Resolve(candidates.Where(p => p.SkuId == i.SkuId).ToList(), now), i.Qty))
            .ToList();
        var gramsByProduct = merged
            .GroupBy(i => skus[i.SkuId].ProductId)
            .Select(g => (ProductId: g.Key, Grams: g.Sum(i => skus[i.SkuId].SizeG * i.Qty)))
            .OrderBy(p => p.ProductId)
            .ToList();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var order = Order.Place(customerId, lines, idempotencyKey, now);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Ascending product order keeps concurrent orders from deadlocking on inventory rows.
            foreach (var (productId, grams) in gramsByProduct)
            {
                var reserved = await db.Inventories
                    .Where(i => i.ProductId == productId && i.StockG >= grams)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockG, i => i.StockG - grams), ct);

                if (reserved == 0)
                {
                    var available = await db.Inventories.Where(i => i.ProductId == productId)
                        .Select(i => (int?)i.StockG).SingleOrDefaultAsync(ct) ?? 0;
                    return new CreateOrderResult.InsufficientStock(productId, grams, available);
                }
            }

            db.Orders.Add(order);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (idempotencyKey is not null && ex.IsUniqueViolation())
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return new CreateOrderResult.Replayed((await FindByKeyAsync(customerId, idempotencyKey, ct))!);
            }

            db.InventoryMovements.AddRange(
                gramsByProduct.Select(p => InventoryMovement.Reserve(p.ProductId, p.Grams, order.Id, now)));
            AddToOutbox(new OrderCreated(order.Id, order.CustomerId, order.Total, order.Currency, now), now);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return (CreateOrderResult)new CreateOrderResult.Created(order);
        });
    }

    public Task<OrderActionResult> PayAsync(string userId, int orderId, CancellationToken ct) =>
        ChangeOwnOrderAsync(userId, orderId, order =>
        {
            if (order.Pay())
            {
                var now = clock.GetUtcNow();
                AddToOutbox(new OrderPaid(order.Id, order.CustomerId, order.Total, order.Currency, now), now);
            }

            return Task.CompletedTask;
        }, ct);

    public Task<OrderActionResult> CancelAsync(string userId, int orderId, CancellationToken ct) =>
        ChangeOwnOrderAsync(userId, orderId, async order =>
        {
            if (order.Cancel())
            {
                await ReleaseStockAsync(order, ct);
                var now = clock.GetUtcNow();
                AddToOutbox(new OrderCancelled(order.Id, order.CustomerId, order.Total, order.Currency, now), now);
            }
        }, ct);

    /// <summary>Stages the message in the current unit of work, so it commits or rolls back with the state change.</summary>
    private void AddToOutbox<TMessage>(TMessage message, DateTimeOffset occurredAt) where TMessage : notnull =>
        db.OutboxMessages.Add(OutboxMessage.For(message, occurredAt));

    public async Task<Order?> GetAsync(string userId, bool isAdmin, int orderId, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.Id == orderId);

        if (!isAdmin)
        {
            if (await FindCustomerIdAsync(userId, ct) is not { } customerId)
                return null;
            query = query.Where(o => o.CustomerId == customerId);
        }

        return await query.SingleOrDefaultAsync(ct);
    }

    public async Task<OrderPage> ListAsync(string userId, int page, int pageSize, CancellationToken ct)
    {
        if (await FindCustomerIdAsync(userId, ct) is not { } customerId)
            return new OrderPage([], page, pageSize, 0);

        var own = db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId);
        var totalCount = await own.CountAsync(ct);

        // Computed in long: a huge page number would otherwise overflow into a negative OFFSET.
        var skip = (long)(page - 1) * pageSize;
        if (skip >= totalCount)
            return new OrderPage([], page, pageSize, totalCount);

        var items = await own.Include(o => o.Lines)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((int)skip).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new OrderPage(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Loads the customer's order, applies the change and saves it in one transaction. The order's row version
    /// detects a concurrent change; the whole step then runs again on fresh data.
    /// </summary>
    private async Task<OrderActionResult> ChangeOwnOrderAsync(
        string userId, int orderId, Func<Order, Task> change, CancellationToken ct)
    {
        if (await FindCustomerIdAsync(userId, ct) is not { } customerId)
            return new OrderActionResult.NotFound();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                var order = await db.Orders.Include(o => o.Lines)
                    .SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
                if (order is null)
                    return (OrderActionResult)new OrderActionResult.NotFound();

                await change(order);

                try
                {
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return new OrderActionResult.Done(order);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // The transaction rolls back on dispose, undoing any stock released in this attempt.
                    if (attempt == MaxConcurrencyAttempts)
                        return new OrderActionResult.Conflict();
                }
            }
        });
    }

    /// <summary>Releases exactly what the order's movements still hold, so stock always matches the ledger.</summary>
    private async Task ReleaseStockAsync(Order order, CancellationToken ct)
    {
        var heldByProduct = await db.InventoryMovements
            .Where(m => m.OrderId == order.Id)
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Grams = -g.Sum(m => m.DeltaG) })
            .Where(p => p.Grams > 0)
            .OrderBy(p => p.ProductId)
            .ToListAsync(ct);
        var now = clock.GetUtcNow();

        foreach (var (productId, grams) in heldByProduct.Select(p => (p.ProductId, p.Grams)))
        {
            var released = await db.Inventories
                .Where(i => i.ProductId == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockG, i => i.StockG + grams), ct);
            if (released == 0)
                throw new InvalidOperationException($"Product {productId} has no inventory row to release stock into.");

            db.InventoryMovements.Add(InventoryMovement.Release(productId, grams, order.Id, now));
        }
    }

    private Task<int?> FindCustomerIdAsync(string userId, CancellationToken ct) =>
        db.Customers.Where(c => c.IdentityUserId == userId).Select(c => (int?)c.Id).SingleOrDefaultAsync(ct);

    private Task<Order?> FindByKeyAsync(int customerId, string idempotencyKey, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey, ct);

    private static string? Validate(IReadOnlyList<OrderItem> items)
    {
        if (items.Count is 0 or > OrderConstraints.MaxLines)
            return $"items must have between 1 and {OrderConstraints.MaxLines} lines.";
        if (items.Any(i => i.Qty is < 1 or > OrderConstraints.MaxQty))
            return $"qty must be between 1 and {OrderConstraints.MaxQty}.";

        return null;
    }
}
