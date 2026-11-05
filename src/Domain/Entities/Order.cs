namespace CoffeeShop.Domain.Entities;

public class Order
{
    private const decimal MaxTotalExclusive = 10_000_000_000_000_000m;

    private Order() { }

    public int Id { get; private set; }
    public int CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }
    public string Currency { get; private set; } = null!;
    public string? IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<OrderLine> Lines { get; private set; } = [];

    /// <summary>Creates a Pending order whose lines freeze the given prices. All prices must share one currency.</summary>
    public static Order Place(
        int customerId,
        IReadOnlyCollection<(EffectivePrice Price, int Qty)> lines,
        string? idempotencyKey,
        DateTimeOffset createdAt)
    {
        if (lines.Count == 0)
            throw new InvalidOrderException("An order needs at least one line.");
        if (lines.Select(l => l.Price.SkuId).Distinct().Count() != lines.Count)
            throw new InvalidOrderException("Each SKU can appear in only one line.");

        var currencies = lines.Select(l => l.Price.Currency).Distinct().Order().ToList();
        if (currencies.Count > 1)
            throw new InvalidOrderException($"All lines must share one currency, got {string.Join(", ", currencies)}.");

        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            Currency = currencies[0],
            IdempotencyKey = idempotencyKey,
            CreatedAt = createdAt,
        };
        order.Lines.AddRange(lines.Select(l => new OrderLine(0, 0, l.Price.SkuId, l.Qty, l.Price.Amount, l.Price.Id)));
        order.Total = order.Lines.Sum(l => l.UnitPriceSnapshot * l.Qty);

        if (order.Total >= MaxTotalExclusive)
            throw new InvalidOrderException($"Order total {order.Total} exceeds the supported maximum.");

        return order;
    }

    /// <summary>Returns false when the order is already paid.</summary>
    public bool Pay()
    {
        if (Status == OrderStatus.Paid)
            return false;

        MoveTo(OrderStatus.Paid, OrderStatus.Pending);
        return true;
    }

    public void StartPreparing() => MoveTo(OrderStatus.Preparing, OrderStatus.Paid);

    public void Ship() => MoveTo(OrderStatus.Shipped, OrderStatus.Preparing);

    public void Deliver() => MoveTo(OrderStatus.Delivered, OrderStatus.Shipped);

    /// <summary>Returns false when the order is already cancelled; the caller releases stock only on true.</summary>
    public bool Cancel()
    {
        if (Status == OrderStatus.Cancelled)
            return false;

        MoveTo(OrderStatus.Cancelled, OrderStatus.Pending, OrderStatus.Paid);
        return true;
    }

    private void MoveTo(OrderStatus target, params OrderStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
            throw new InvalidOrderTransitionException(Status, target);

        Status = target;
    }
}

public sealed class InvalidOrderException(string message) : DomainException(message);

public sealed class InvalidOrderTransitionException(OrderStatus from, OrderStatus to)
    : DomainException($"An order cannot go from {from} to {to}.");
