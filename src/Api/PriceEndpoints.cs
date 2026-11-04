using CoffeeShop.Application;
using CoffeeShop.Application.Auth;

namespace CoffeeShop.Api;

public static class PriceEndpoints
{
    public static void MapPriceEndpoints(this IEndpointRouteBuilder app)
    {
        var prices = app.MapGroup("").WithTags("Prices").RequireAuthorization(AuthPolicies.AdminOnly);

        prices.MapPost("/prices", CreatePrice)
            .WithName("CreatePrice")
            .WithSummary("Appends a price to a SKU, effective today (UTC) or later. Requires the Admin role.")
            .Produces<PriceCreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        prices.MapGet("/skus/{id:int}/prices", GetPriceHistory)
            .WithName("GetPriceHistory")
            .WithSummary("Returns the full price history of a SKU, latest first. Requires the Admin role.")
            .Produces<IReadOnlyList<PriceHistoryResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreatePrice(CreatePriceRequest request, PriceService prices, CancellationToken ct)
    {
        if (request is not { SkuId: { } skuId, Amount: { } amount, Currency: { } currency, EffectiveFrom: { } effectiveFromText })
            return Invalid("skuId, amount, currency and effectiveFrom are required.");
        if (!IsoDateTime.TryParse(effectiveFromText, out var effectiveFrom))
            return Invalid("effectiveFrom must be ISO 8601, e.g. 2030-01-01 or 2030-01-01T00:00:00Z. Values without offset are UTC.");

        var result = await prices.CreateAsync(new CreatePriceCommand(skuId, amount, currency, effectiveFrom), ct);

        return result switch
        {
            CreatePriceResult.Created created => Results.Created(
                $"/skus/{skuId}/prices",
                new PriceCreatedResponse(created.Price.Id, skuId, created.Price.Amount, created.Price.Currency, created.Price.EffectiveFrom)),
            CreatePriceResult.Invalid invalid => Invalid(invalid.Message),
            CreatePriceResult.PastDate => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Effective date in the past",
                detail: "effectiveFrom must be today (UTC) or later."),
            CreatePriceResult.SkuNotFound => SkuNotFound(skuId),
            CreatePriceResult.Duplicate => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate price",
                detail: $"SKU {skuId} already has a price effective at that instant."),
            _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}."),
        };
    }

    private static async Task<IResult> GetPriceHistory(int id, PriceService prices, CancellationToken ct)
    {
        var history = await prices.GetHistoryAsync(id, ct);

        return history is null
            ? SkuNotFound(id)
            : Results.Ok(history.Select(item => new PriceHistoryResponse(
                item.Price.Id, item.Price.Amount, item.Price.Currency, item.Price.EffectiveFrom, item.IsCurrent)));
    }

    private static IResult SkuNotFound(int id) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "SKU not found", detail: $"SKU {id} does not exist.");

    private static IResult Invalid(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = [message] });
}

public sealed record CreatePriceRequest(int? SkuId, decimal? Amount, string? Currency, string? EffectiveFrom);

public sealed record PriceCreatedResponse(int Id, int SkuId, decimal Amount, string Currency, DateTimeOffset EffectiveFrom);

public sealed record PriceHistoryResponse(int Id, decimal Amount, string Currency, DateTimeOffset EffectiveFrom, bool IsCurrent);
