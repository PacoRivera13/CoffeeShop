using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Domain.Pricing;

public static class PriceResolver
{
    /// <summary>Returns the row with the greatest EffectiveFrom not after asOf. All prices must belong to one SKU.</summary>
    public static EffectivePrice Resolve(IReadOnlyCollection<EffectivePrice> prices, DateTimeOffset asOf)
    {
        if (prices.Select(p => p.SkuId).Distinct().Skip(1).Any())
            throw new ArgumentException("All prices must belong to the same SKU.", nameof(prices));

        return prices.Where(p => p.EffectiveFrom <= asOf).MaxBy(p => p.EffectiveFrom)
            ?? throw new NoEffectivePriceException(asOf);
    }
}

public sealed class NoEffectivePriceException(DateTimeOffset asOf)
    : DomainException($"No effective price as of {asOf:O}.")
{
    public DateTimeOffset AsOf { get; } = asOf;
}
