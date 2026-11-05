using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Domain.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static EffectivePrice Price(int id, int skuId, decimal amount, string currency = "EUR") =>
        new(id, skuId, amount, currency, Now.AddDays(-1));

    private static Order NewOrder() => Order.Place(7, [(Price(1, 1, 12.50m), 2), (Price(2, 2, 20m), 1)], "key", Now);

    private static Order OrderIn(OrderStatus status)
    {
        var order = NewOrder();
        switch (status)
        {
            case OrderStatus.Paid: order.Pay(); break;
            case OrderStatus.Preparing: order.Pay(); order.StartPreparing(); break;
            case OrderStatus.Shipped: order.Pay(); order.StartPreparing(); order.Ship(); break;
            case OrderStatus.Delivered: order.Pay(); order.StartPreparing(); order.Ship(); order.Deliver(); break;
            case OrderStatus.Cancelled: order.Cancel(); break;
        }

        Assert.Equal(status, order.Status);
        return order;
    }

    [Fact]
    public void Place_freezes_prices_and_computes_the_total()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(45m, order.Total);
        Assert.Equal("EUR", order.Currency);
        Assert.Equal("key", order.IdempotencyKey);
        Assert.Equal([(1, 2, 12.50m, 1), (2, 1, 20m, 2)], order.Lines.Select(l => (l.SkuId, l.Qty, l.UnitPriceSnapshot, l.PriceId)));
    }

    [Fact]
    public void Place_rejects_mixed_currencies() =>
        Assert.Throws<InvalidOrderException>(() => Order.Place(7, [(Price(1, 1, 10m), 1), (Price(2, 2, 10m, "USD"), 1)], null, Now));

    [Fact]
    public void Place_rejects_an_empty_order() =>
        Assert.Throws<InvalidOrderException>(() => Order.Place(7, [], null, Now));

    [Fact]
    public void Place_rejects_repeated_skus() =>
        Assert.Throws<InvalidOrderException>(() => Order.Place(7, [(Price(1, 1, 10m), 1), (Price(2, 1, 11m), 1)], null, Now));

    [Fact]
    public void Order_follows_the_happy_path()
    {
        var order = NewOrder();

        Assert.True(order.Pay());
        order.StartPreparing();
        order.Ship();
        order.Deliver();

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void Paying_twice_is_a_no_op() => Assert.False(OrderIn(OrderStatus.Paid).Pay());

    [Fact]
    public void Cancelling_twice_is_a_no_op() => Assert.False(OrderIn(OrderStatus.Cancelled).Cancel());

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Paid)]
    public void Cancel_is_allowed_before_preparation(OrderStatus status)
    {
        var order = OrderIn(status);

        Assert.True(order.Cancel());
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public void Cancel_is_rejected_once_preparation_started(OrderStatus status) =>
        Assert.Throws<InvalidOrderTransitionException>(() => OrderIn(status).Cancel());

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Preparing)]
    public void Pay_is_rejected_after_cancel_or_preparation(OrderStatus status) =>
        Assert.Throws<InvalidOrderTransitionException>(() => OrderIn(status).Pay());

    [Fact]
    public void Steps_cannot_be_skipped()
    {
        Assert.Throws<InvalidOrderTransitionException>(() => NewOrder().StartPreparing());
        Assert.Throws<InvalidOrderTransitionException>(() => OrderIn(OrderStatus.Paid).Ship());
        Assert.Throws<InvalidOrderTransitionException>(() => OrderIn(OrderStatus.Preparing).Deliver());
    }

    [Fact]
    public void Movements_carry_the_sign_of_their_reason()
    {
        Assert.Equal(-500, InventoryMovement.Reserve(1, 500, 3, Now).DeltaG);
        Assert.Equal(500, InventoryMovement.Release(1, 500, 3, Now).DeltaG);
        Assert.Equal(500, InventoryMovement.Restock(1, 500, Now).DeltaG);
        Assert.Throws<InvalidInventoryMovementException>(() => InventoryMovement.Reserve(1, 0, 3, Now));
    }
}
