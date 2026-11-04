using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Pricing;

namespace CoffeeShop.Application;

public interface IPriceStore
{
    /// <summary>Inserts the price; returns false when the database reports a duplicate (sku, effective date).</summary>
    Task<bool> TryAddAsync(EffectivePrice price, CancellationToken ct);

    /// <summary>Returns every price of the SKU, latest EffectiveFrom first.</summary>
    Task<IReadOnlyList<EffectivePrice>> GetHistoryAsync(int skuId, CancellationToken ct);
}

public sealed record CreatePriceCommand(int SkuId, decimal Amount, string Currency, DateTimeOffset EffectiveFrom);

public abstract record CreatePriceResult
{
    public sealed record Created(EffectivePrice Price) : CreatePriceResult;

    public sealed record Invalid(string Message) : CreatePriceResult;

    public sealed record PastDate : CreatePriceResult;

    public sealed record SkuNotFound : CreatePriceResult;

    public sealed record Duplicate : CreatePriceResult;
}

public sealed record PriceHistoryItem(EffectivePrice Price, bool IsCurrent);

public sealed class PriceService(ICatalogReadStore catalog, IPriceStore prices, TimeProvider clock)
{
    public async Task<CreatePriceResult> CreateAsync(CreatePriceCommand command, CancellationToken ct)
    {
        EffectivePrice price;
        try
        {
            price = new EffectivePrice(0, command.SkuId, command.Amount, command.Currency, command.EffectiveFrom);
        }
        catch (InvalidPriceException ex)
        {
            return new CreatePriceResult.Invalid(ex.Message);
        }

        if (command.EffectiveFrom.UtcDateTime.Date < clock.GetUtcNow().UtcDateTime.Date)
            return new CreatePriceResult.PastDate();

        if (!await catalog.SkuExistsAsync(command.SkuId, ct))
            return new CreatePriceResult.SkuNotFound();

        return await prices.TryAddAsync(price, ct)
            ? new CreatePriceResult.Created(price)
            : new CreatePriceResult.Duplicate();
    }

    /// <summary>Returns null when the SKU does not exist; no item is current when no price applies yet.</summary>
    public async Task<IReadOnlyList<PriceHistoryItem>?> GetHistoryAsync(int skuId, CancellationToken ct)
    {
        var history = await prices.GetHistoryAsync(skuId, ct);

        if (history.Count == 0 && !await catalog.SkuExistsAsync(skuId, ct))
            return null;

        var current = ResolveCurrent(history);
        return history.Select(p => new PriceHistoryItem(p, p == current)).ToList();
    }

    private EffectivePrice? ResolveCurrent(IReadOnlyList<EffectivePrice> history)
    {
        try
        {
            return PriceResolver.Resolve(history, clock.GetUtcNow());
        }
        catch (NoEffectivePriceException)
        {
            return null;
        }
    }
}
