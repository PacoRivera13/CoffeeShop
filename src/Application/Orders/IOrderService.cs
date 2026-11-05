using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Application.Orders;

public static class OrderConstraints
{
    public const int MaxLines = 20;
    public const int MaxQty = 50;
    public const int MaxIdempotencyKeyLength = 100;
    public const int MaxPageSize = 100;
}

public sealed record OrderItem(int SkuId, int Qty);

public abstract record CreateOrderResult
{
    public sealed record Created(Order Order) : CreateOrderResult;

    /// <summary>The Idempotency-Key was already used by this customer; no side effects were applied.</summary>
    public sealed record Replayed(Order Order) : CreateOrderResult;

    public sealed record Invalid(string Message) : CreateOrderResult;

    public sealed record CustomerNotFound : CreateOrderResult;

    public sealed record SkusNotFound(IReadOnlyList<int> SkuIds) : CreateOrderResult;

    public sealed record NoEffectivePrice(IReadOnlyList<int> SkuIds) : CreateOrderResult;

    public sealed record InsufficientStock(int ProductId, int RequiredG, int AvailableG) : CreateOrderResult;
}

public abstract record OrderActionResult
{
    public sealed record Done(Order Order) : OrderActionResult;

    public sealed record NotFound : OrderActionResult;
}

public sealed record OrderPage(IReadOnlyList<Order> Items, int Page, int PageSize, int TotalCount);

/// <summary>Orders of the customer linked to an Identity user; another customer's order behaves as not found.</summary>
public interface IOrderService
{
    Task<CreateOrderResult> CreateAsync(string userId, IReadOnlyList<OrderItem> items, string? idempotencyKey, CancellationToken ct);

    Task<OrderActionResult> PayAsync(string userId, int orderId, CancellationToken ct);

    Task<OrderActionResult> CancelAsync(string userId, int orderId, CancellationToken ct);

    Task<Order?> GetAsync(string userId, bool isAdmin, int orderId, CancellationToken ct);

    Task<OrderPage> ListAsync(string userId, int page, int pageSize, CancellationToken ct);
}
