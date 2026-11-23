using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Application.Inventory;

public static class InventoryConstraints
{
    public const int MaxRestockGrams = 1_000_000;
    public const int MaxPageSize = 100;
}

public sealed record InventoryItem(int ProductId, string Name, int StockG, DateTimeOffset? LastMovementAt);

public abstract record RestockResult
{
    public sealed record Done(int ProductId, int StockG, InventoryMovement Movement) : RestockResult;

    public sealed record Invalid(string Message) : RestockResult;

    public sealed record ProductNotFound : RestockResult;

    /// <summary>The restock would push the stock past what the stock column can hold; nothing was written.</summary>
    public sealed record StockLimitExceeded(int StockG) : RestockResult;
}

public sealed record MovementPage(IReadOnlyList<InventoryMovement> Items, int Page, int PageSize, int TotalCount);

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItem>> ListAsync(CancellationToken ct);

    Task<RestockResult> RestockAsync(int productId, int grams, CancellationToken ct);

    /// <summary>Returns null when the product does not exist.</summary>
    Task<MovementPage?> GetMovementsAsync(int productId, int page, int pageSize, CancellationToken ct);
}
