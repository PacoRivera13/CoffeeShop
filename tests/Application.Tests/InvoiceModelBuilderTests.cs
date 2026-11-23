using CoffeeShop.Application.Invoicing;
using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Application.Tests;

public class InvoiceModelBuilderTests
{
    private static readonly DateTimeOffset PlacedAt = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 21, 9, 30, 0, TimeSpan.Zero);

    private static readonly Customer Alice = new(7, "Alice", "alice@example.test", "user-7");

    private static readonly Dictionary<int, InvoiceSku> Skus = new()
    {
        [1] = new InvoiceSku("Yirgacheffe", Grind.WholeBean, 500),
        [2] = new InvoiceSku("Huila", Grind.Ground, 1000),
    };

    private static Order NewOrder() => Order.Place(
        Alice.Id,
        [
            (new EffectivePrice(10, 1, 12.50m, "MXN", PlacedAt.AddDays(-1)), 2),
            (new EffectivePrice(20, 2, 18.00m, "MXN", PlacedAt.AddDays(-1)), 3),
        ],
        null,
        PlacedAt);

    [Fact]
    public void Lines_and_total_come_from_the_order_snapshots()
    {
        var invoice = InvoiceModelBuilder.Build(NewOrder(), Alice, Skus, "INV-2026-000001", IssuedAt);

        Assert.Equal(
            [
                new InvoiceLine("Yirgacheffe", Grind.WholeBean, 500, 2, 12.50m, 25.00m),
                new InvoiceLine("Huila", Grind.Ground, 1000, 3, 18.00m, 54.00m),
            ],
            invoice.Lines);
        Assert.Equal(79.00m, invoice.Total);
        Assert.Equal(("INV-2026-000001", IssuedAt, "Alice", "alice@example.test", "MXN"),
            (invoice.InvoiceNumber, invoice.IssuedAt, invoice.CustomerName, invoice.CustomerEmail, invoice.Currency));
    }

    [Fact]
    public void Current_prices_play_no_part()
    {
        var order = NewOrder();

        // A later price change is not an input: the builder only sees the frozen UnitPriceSnapshot of each line.
        var invoice = InvoiceModelBuilder.Build(order, Alice, Skus, "INV-2026-000002", IssuedAt.AddYears(1));

        Assert.Equal(order.Lines.Select(l => l.UnitPriceSnapshot).Order(), invoice.Lines.Select(l => l.UnitPrice).Order());
        Assert.Equal(order.Total, invoice.Total);
    }

    [Fact]
    public void Another_customer_is_rejected() =>
        Assert.Throws<ArgumentException>(() =>
            InvoiceModelBuilder.Build(NewOrder(), new Customer(8, "Bob", "bob@example.test", "user-8"), Skus, "INV", IssuedAt));

    [Fact]
    public void A_missing_sku_is_rejected() =>
        Assert.Throws<ArgumentException>(() =>
            InvoiceModelBuilder.Build(NewOrder(), Alice, new Dictionary<int, InvoiceSku> { [1] = Skus[1] }, "INV", IssuedAt));

    [Fact]
    public void Invoice_numbers_use_the_utc_year_and_six_digits()
    {
        Assert.Equal("INV-2026-000042", Invoice.FormatNumber(42, new DateTimeOffset(2026, 12, 31, 23, 0, 0, TimeSpan.Zero)));
        // 2026-12-31 22:30 at UTC-2 is already 2027 in UTC.
        Assert.Equal("INV-2027-000042", Invoice.FormatNumber(42, new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.FromHours(-2))));
        Assert.Equal("invoices/15.pdf", Invoice.StorageKeyFor(15));
    }
}
