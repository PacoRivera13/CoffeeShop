using System.Globalization;
using CoffeeShop.Application;

namespace CoffeeShop.Api;

public static class CatalogEndpoints
{
    private static readonly string[] AsOfFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
    ];

    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", GetProducts);
        app.MapGet("/skus/{id:int}/price", GetSkuPrice);
    }

    private static async Task<IResult> GetProducts(CatalogService catalog, CancellationToken ct)
    {
        var products = await catalog.GetProductsAsync(ct);

        return Results.Ok(products.Select(p => new ProductResponse(
            p.Id, p.Name, p.Origin, p.Roast,
            p.Skus.Select(s => new SkuResponse(s.Id, s.Grind.ToString(), s.SizeG)).ToList())));
    }

    private static async Task<IResult> GetSkuPrice(
        int id, string? asOf, CatalogService catalog, TimeProvider clock, CancellationToken ct)
    {
        var instant = clock.GetUtcNow();
        if (asOf is not null && !TryParseAsOf(asOf, out instant))
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

    private static bool TryParseAsOf(string value, out DateTimeOffset asOf) =>
        DateTimeOffset.TryParseExact(value, AsOfFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out asOf);
}

public sealed record ProductResponse(int Id, string Name, string Origin, string Roast, IReadOnlyList<SkuResponse> Skus);

public sealed record SkuResponse(int Id, string Grind, int SizeG);

public sealed record PriceResponse(int SkuId, int PriceId, decimal Amount, string Currency, DateTimeOffset EffectiveFrom, DateTimeOffset AsOf);
