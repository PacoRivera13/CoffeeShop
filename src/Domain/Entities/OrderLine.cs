namespace CoffeeShop.Domain.Entities;

public class OrderLine
{
    private OrderLine() { }

    public OrderLine(int id, int orderId, int skuId, int qty, decimal unitPriceSnapshot, int priceId)
    {
        if (qty <= 0)
            throw new InvalidOrderLineException($"Quantity must be positive, got {qty}.");
        if (unitPriceSnapshot < 0)
            throw new InvalidOrderLineException($"Unit price cannot be negative, got {unitPriceSnapshot}.");

        Id = id;
        OrderId = orderId;
        SkuId = skuId;
        Qty = qty;
        UnitPriceSnapshot = unitPriceSnapshot;
        PriceId = priceId;
    }

    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public int SkuId { get; private set; }
    public int Qty { get; private set; }
    public decimal UnitPriceSnapshot { get; private set; }
    public int PriceId { get; private set; }
}

public sealed class InvalidOrderLineException(string message) : DomainException(message);
