using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Pricing;

namespace CoffeeShop.Application;

public interface ICatalogReadStore
{
    Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct);

    Task<bool> SkuExistsAsync(int skuId, CancellationToken ct);

    /// <summary>Returns the SKU's prices with EffectiveFrom not after asOf; PriceResolver picks the applicable one.</summary>
    Task<IReadOnlyList<EffectivePrice>> GetPricesAsync(int skuId, DateTimeOffset asOf, CancellationToken ct);
}

public sealed class CatalogService(ICatalogReadStore store)
{
    public Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct) => store.GetProductsAsync(ct);

    /// <summary>Returns null when the SKU does not exist; throws NoEffectivePriceException when it has no price yet.</summary>
    public async Task<EffectivePrice?> GetPriceAsync(int skuId, DateTimeOffset asOf, CancellationToken ct)
    {
        var prices = await store.GetPricesAsync(skuId, asOf, ct);

        if (prices.Count == 0 && !await store.SkuExistsAsync(skuId, ct))
            return null;

        return PriceResolver.Resolve(prices, asOf);
    }
}
