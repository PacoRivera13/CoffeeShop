namespace CoffeeShop.Domain.Entities;

public class Order
{
    private Order() { }

    public Order(int id, int customerId, DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        CreatedAt = createdAt;
        Status = OrderStatus.Pending;
    }

    public int Id { get; private set; }
    public int CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<OrderLine> Lines { get; private set; } = [];
}
