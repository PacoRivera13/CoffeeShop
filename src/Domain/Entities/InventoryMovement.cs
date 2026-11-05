namespace CoffeeShop.Domain.Entities;

/// <summary>Append-only record of a stock change; the sum of DeltaG per product equals its stock.</summary>
public class InventoryMovement
{
    private InventoryMovement() { }

    private InventoryMovement(int productId, int deltaG, MovementReason reason, int? orderId, DateTimeOffset createdAt)
    {
        ProductId = productId;
        DeltaG = deltaG;
        Reason = reason;
        OrderId = orderId;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int DeltaG { get; private set; }
    public MovementReason Reason { get; private set; }
    public int? OrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static InventoryMovement Reserve(int productId, int grams, int orderId, DateTimeOffset at) =>
        new(productId, -Positive(grams), MovementReason.Reserve, orderId, at);

    public static InventoryMovement Release(int productId, int grams, int orderId, DateTimeOffset at) =>
        new(productId, Positive(grams), MovementReason.Release, orderId, at);

    public static InventoryMovement Restock(int productId, int grams, DateTimeOffset at) =>
        new(productId, Positive(grams), MovementReason.Restock, null, at);

    private static int Positive(int grams) =>
        grams > 0 ? grams : throw new InvalidInventoryMovementException($"Grams must be positive, got {grams}.");
}

public sealed class InvalidInventoryMovementException(string message) : DomainException(message);
