using System.Security.Claims;
using CoffeeShop.Application.Auth;
using CoffeeShop.Application.Orders;
using CoffeeShop.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoffeeShop.Api;

public static class OrderEndpoints
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders").RequireAuthorization();

        orders.MapPost("", CreateOrder)
            .RequireAuthorization(AuthPolicies.CustomerOnly)
            .WithName("CreateOrder")
            .WithSummary("Places an order at current prices and reserves its stock. Repeating an Idempotency-Key returns the original order.")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .Produces<OrderResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        orders.MapGet("", ListOrders)
            .WithName("ListOrders")
            .WithSummary("Lists the caller's orders, newest first.")
            .Produces<OrderPageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        orders.MapGet("/{id:int}", GetOrder)
            .WithName("GetOrder")
            .WithSummary("Returns an order of the caller, or any order for an Admin.")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        orders.MapPost("/{id:int}/pay", PayOrder)
            .WithName("PayOrder")
            .WithSummary("Simulates the payment of the caller's order. Paying a paid order is a no-op.")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        orders.MapPost("/{id:int}/cancel", CancelOrder)
            .WithName("CancelOrder")
            .WithSummary("Cancels the caller's Pending or Paid order and releases its stock. Cancelling twice is a no-op.")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        ClaimsPrincipal user,
        IOrderService orders,
        CancellationToken ct)
    {
        if (request.Items is null || request.Items.Any(i => i?.SkuId is null || i.Qty is null))
            return Invalid("items is required and every item needs skuId and qty.");

        var items = request.Items.Select(i => new OrderItem(i!.SkuId!.Value, i.Qty!.Value)).ToList();
        var result = await orders.CreateAsync(UserId(user), items, idempotencyKey, ct);

        return result switch
        {
            CreateOrderResult.Created created => Results.Created($"/orders/{created.Order.Id}", ToResponse(created.Order)),
            CreateOrderResult.Replayed replayed => Results.Ok(ToResponse(replayed.Order)),
            CreateOrderResult.Invalid invalid => Invalid(invalid.Message),
            CreateOrderResult.CustomerNotFound => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "No customer profile",
                detail: "The authenticated user has no customer record."),
            CreateOrderResult.SkusNotFound missing => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "SKU not found",
                detail: $"Unknown SKUs: {string.Join(", ", missing.SkuIds)}."),
            CreateOrderResult.NoEffectivePrice unpriced => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "No effective price",
                detail: $"SKUs without a current price: {string.Join(", ", unpriced.SkuIds)}."),
            CreateOrderResult.InsufficientStock stock => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Insufficient stock",
                detail: $"Product {stock.ProductId} needs {stock.RequiredG} g but only {stock.AvailableG} g are available.",
                extensions: new Dictionary<string, object?>
                {
                    ["productId"] = stock.ProductId,
                    ["requiredG"] = stock.RequiredG,
                    ["availableG"] = stock.AvailableG,
                }),
            _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}."),
        };
    }

    private static async Task<IResult> ListOrders(
        ClaimsPrincipal user, IOrderService orders, CancellationToken ct, int page = 1, int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > OrderConstraints.MaxPageSize)
            return Invalid($"page must be 1 or more and pageSize between 1 and {OrderConstraints.MaxPageSize}.");

        var result = await orders.ListAsync(UserId(user), page, pageSize, ct);

        return Results.Ok(new OrderPageResponse(
            result.Items.Select(ToResponse).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> GetOrder(int id, ClaimsPrincipal user, IOrderService orders, CancellationToken ct)
    {
        var order = await orders.GetAsync(UserId(user), user.IsInRole(AppRoles.Admin), id, ct);

        return order is null ? OrderNotFound(id) : Results.Ok(ToResponse(order));
    }

    private static async Task<IResult> PayOrder(int id, ClaimsPrincipal user, IOrderService orders, CancellationToken ct) =>
        ToResult(id, await orders.PayAsync(UserId(user), id, ct));

    private static async Task<IResult> CancelOrder(int id, ClaimsPrincipal user, IOrderService orders, CancellationToken ct) =>
        ToResult(id, await orders.CancelAsync(UserId(user), id, ct));

    private static IResult ToResult(int id, OrderActionResult result) => result switch
    {
        OrderActionResult.Done done => Results.Ok(ToResponse(done.Order)),
        OrderActionResult.NotFound => OrderNotFound(id),
        _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}."),
    };

    private static OrderResponse ToResponse(Order order) => new(
        order.Id,
        order.Status.ToString(),
        order.Total,
        order.Currency,
        order.CreatedAt,
        order.Lines.OrderBy(l => l.SkuId)
            .Select(l => new OrderLineResponse(l.SkuId, l.Qty, l.UnitPriceSnapshot, l.PriceId, l.UnitPriceSnapshot * l.Qty))
            .ToList());

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private static IResult OrderNotFound(int id) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found", detail: $"Order {id} does not exist.");

    private static IResult Invalid(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = [message] });
}

public sealed record CreateOrderRequest(IReadOnlyList<CreateOrderItemRequest?>? Items);

public sealed record CreateOrderItemRequest(int? SkuId, int? Qty);

public sealed record OrderResponse(
    int Id, string Status, decimal Total, string Currency, DateTimeOffset CreatedAt, IReadOnlyList<OrderLineResponse> Lines);

public sealed record OrderLineResponse(int SkuId, int Qty, decimal UnitPrice, int PriceId, decimal LineTotal);

public sealed record OrderPageResponse(IReadOnlyList<OrderResponse> Items, int Page, int PageSize, int TotalCount);
