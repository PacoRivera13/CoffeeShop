using System.ComponentModel;
using CoffeeShop.Application;

namespace CoffeeShop.Api;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var catalog = app.MapGroup("").WithTags("Catalog");

        catalog.MapGet("/products", GetProducts)
            .WithName("GetProducts")
            .WithSummary("Lists products with their SKUs.")
            .Produces<IReadOnlyList<ProductResponse>>();

        catalog.MapGet("/skus/{id:int}/price", GetSkuPrice)
            .WithName("GetSkuPrice")
            .WithSummary("Returns the price of a SKU effective at a given instant.")
            .Produces<PriceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetProducts(CatalogService catalog, CancellationToken ct)
    {
        var products = await catalog.GetProductsAsync(ct);

        return Results.Ok(products.Select(p => new ProductResponse(
            p.Id, p.Name, p.Origin, p.Roast,
            p.Skus.Select(s => new SkuResponse(s.Id, s.Grind.ToString(), s.SizeG)).ToList())));
    }

    private static async Task<IResult> GetSkuPrice(
        int id,
        [Description("ISO 8601 date or date-time, read as UTC without an offset. Defaults to the current time.")] string? asOf,
        CatalogService catalog,
        TimeProvider clock,
        CancellationToken ct)
    {
        var instant = clock.GetUtcNow();
        if (asOf is not null && !IsoDateTime.TryParse(asOf, out instant))
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid asOf",
                detail: "Use ISO 8601, e.g. 2026-06-01 or 2026-06-01T00:00:00Z. Values without offset are UTC; encode '+' as %2B.");

        var price = await catalog.GetPriceAsync(id, instant, ct);

        return price is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "SKU not found",
                detail: $"SKU {id} does not exist.")
            : Results.Ok(new PriceResponse(id, price.Id, price.Amount, price.Currency, price.EffectiveFrom, instant));
    }
}

public sealed record ProductResponse(int Id, string Name, string Origin, string Roast, IReadOnlyList<SkuResponse> Skus);

public sealed record SkuResponse(int Id, string Grind, int SizeG);

public sealed record PriceResponse(int SkuId, int PriceId, decimal Amount, string Currency, DateTimeOffset EffectiveFrom, DateTimeOffset AsOf);
