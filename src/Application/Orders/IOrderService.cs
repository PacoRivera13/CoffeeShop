using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Application.Orders;

public static class OrderConstraints
{
    public const int MaxLines = 20;
    public const int MaxQty = 50;
    public const int MaxIdempotencyKeyLength = 100;
    public const int MaxPageSize = 100;

    /// <summary>How long an invoice download URL stays valid.</summary>
    public static readonly TimeSpan InvoiceUrlLifetime = TimeSpan.FromMinutes(5);
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

    /// <summary>The order kept changing concurrently; nothing was applied.</summary>
    public sealed record Conflict : OrderActionResult;
}

public abstract record InvoiceAccessResult
{
    public sealed record Ready(Uri Url, DateTimeOffset ExpiresAt, string InvoiceNumber) : InvoiceAccessResult;

    /// <summary>The order is paid but its invoice has not been generated yet.</summary>
    public sealed record Pending : InvoiceAccessResult;

    /// <summary>The order is Pending or was cancelled without an invoice: none will be generated.</summary>
    public sealed record NotPaid(OrderStatus Status) : InvoiceAccessResult;

    public sealed record NotFound : InvoiceAccessResult;
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

    /// <summary>Gives the owner, or an Admin, a short-lived download URL for the order's invoice.</summary>
    Task<InvoiceAccessResult> GetInvoiceAsync(string userId, bool isAdmin, int orderId, CancellationToken ct);
}
