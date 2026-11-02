using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Pricing;

namespace CoffeeShop.Application;

public interface ICatalogReadStore
{
    Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct);

    Task<bool> SkuExistsAsync(int skuId, CancellationToken ct);

    Task<IReadOnlyList<EffectivePrice>> GetPricesAsync(int skuId, CancellationToken ct);
}

public sealed class CatalogService(ICatalogReadStore store)
{
    public Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct) => store.GetProductsAsync(ct);

    /// <summary>Returns null when the SKU does not exist; throws NoEffectivePriceException when it has no price yet.</summary>
    public async Task<EffectivePrice?> GetPriceAsync(int skuId, DateTimeOffset asOf, CancellationToken ct)
    {
        if (!await store.SkuExistsAsync(skuId, ct))
            return null;

        return PriceResolver.Resolve(await store.GetPricesAsync(skuId, ct), asOf);
    }
}
