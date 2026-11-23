using CoffeeShop.Application.Auth;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Api;

public static class AdminInventoryEndpoints
{
    public static void MapAdminInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var inventory = app.MapGroup("/admin/inventory")
            .WithTags("Admin inventory")
            .RequireAuthorization(AuthPolicies.AdminOnly);

        inventory.MapGet("", ListInventory)
            .WithName("ListInventory")
            .WithSummary("Lists every product with its stock in grams and its latest movement. Requires the Admin role.")
            .Produces<IReadOnlyList<InventoryResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        inventory.MapPost("/{productId:int}/restock", Restock)
            .WithName("RestockProduct")
            .WithSummary("Adds grams to a product's stock and records a Restock movement. Requires the Admin role.")
            .Produces<RestockResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        inventory.MapGet("/{productId:int}/movements", GetMovements)
            .WithName("GetInventoryMovements")
            .WithSummary("Returns a product's stock ledger, newest first. Requires the Admin role.")
            .Produces<MovementPageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListInventory(IInventoryService inventory, CancellationToken ct) =>
        Results.Ok((await inventory.ListAsync(ct))
            .Select(i => new InventoryResponse(i.ProductId, i.Name, i.StockG, i.LastMovementAt)));

    private static async Task<IResult> Restock(int productId, RestockRequest request, IInventoryService inventory, CancellationToken ct)
    {
        if (request.Grams is not { } grams)
            return Invalid("grams is required.");

        var result = await inventory.RestockAsync(productId, grams, ct);

        return result switch
        {
            RestockResult.Done done => Results.Created(
                $"/admin/inventory/{productId}/movements",
                new RestockResponse(done.ProductId, done.StockG, ToResponse(done.Movement))),
            RestockResult.Invalid invalid => Invalid(invalid.Message),
            RestockResult.ProductNotFound => ProductNotFound(productId),
            RestockResult.StockLimitExceeded limit => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Stock limit exceeded",
                detail: $"Product {productId} has {limit.StockG} g; adding {grams} g would exceed the maximum stock."),
            _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}."),
        };
    }

    private static async Task<IResult> GetMovements(
        int productId, IInventoryService inventory, CancellationToken ct, int page = 1, int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > InventoryConstraints.MaxPageSize)
            return Invalid($"page must be 1 or more and pageSize between 1 and {InventoryConstraints.MaxPageSize}.");

        var result = await inventory.GetMovementsAsync(productId, page, pageSize, ct);

        return result is null
            ? ProductNotFound(productId)
            : Results.Ok(new MovementPageResponse(
                result.Items.Select(ToResponse).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    private static MovementResponse ToResponse(InventoryMovement movement) =>
        new(movement.Id, movement.DeltaG, movement.Reason.ToString(), movement.OrderId, movement.CreatedAt);

    private static IResult ProductNotFound(int productId) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found", detail: $"Product {productId} does not exist.");

    private static IResult Invalid(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = [message] });
}

public sealed record InventoryResponse(int ProductId, string Name, int StockG, DateTimeOffset? LastMovementAt);

public sealed record RestockRequest(int? Grams);

public sealed record MovementResponse(int Id, int DeltaG, string Reason, int? OrderId, DateTimeOffset CreatedAt);

public sealed record RestockResponse(int ProductId, int StockG, MovementResponse Movement);

public sealed record MovementPageResponse(IReadOnlyList<MovementResponse> Items, int Page, int PageSize, int TotalCount);
