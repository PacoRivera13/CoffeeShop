namespace CoffeeShop.Domain.Entities;

public enum Grind
{
    WholeBean = 1,
    Ground = 2
}

public enum OrderStatus
{
    Pending = 1,
    Paid = 2,
    Preparing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6
}

public enum MovementReason
{
    Reserve = 1,
    Release = 2,
    Restock = 3
}
