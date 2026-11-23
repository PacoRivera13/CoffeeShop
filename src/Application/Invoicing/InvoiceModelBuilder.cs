using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Application.Invoicing;

public sealed record InvoiceSku(string ProductName, Grind Grind, int SizeG);

public sealed record InvoiceLine(string ProductName, Grind Grind, int SizeG, int Qty, decimal UnitPrice, decimal LineTotal);

public sealed record InvoiceDocument(
    string InvoiceNumber,
    DateTimeOffset IssuedAt,
    int OrderId,
    string CustomerName,
    string CustomerEmail,
    string Currency,
    IReadOnlyList<InvoiceLine> Lines,
    decimal Total);

/// <summary>Builds the invoice content from an order. Pure: prices come only from the order's frozen snapshots.</summary>
public static class InvoiceModelBuilder
{
    public static InvoiceDocument Build(
        Order order,
        Customer customer,
        IReadOnlyDictionary<int, InvoiceSku> skus,
        string invoiceNumber,
        DateTimeOffset issuedAt)
    {
        if (customer.Id != order.CustomerId)
            throw new ArgumentException($"Customer {customer.Id} does not own order {order.Id}.", nameof(customer));

        var lines = order.Lines
            .OrderBy(l => l.SkuId)
            .Select(l =>
            {
                var sku = skus.TryGetValue(l.SkuId, out var found)
                    ? found
                    : throw new ArgumentException($"SKU {l.SkuId} of order {order.Id} is missing.", nameof(skus));
                return new InvoiceLine(sku.ProductName, sku.Grind, sku.SizeG, l.Qty, l.UnitPriceSnapshot, l.UnitPriceSnapshot * l.Qty);
            })
            .ToList();

        var total = lines.Sum(l => l.LineTotal);
        if (total != order.Total)
            throw new InvalidOperationException($"Order {order.Id} lines add up to {total}, but its total is {order.Total}.");

        return new InvoiceDocument(
            invoiceNumber, issuedAt, order.Id, customer.Name, customer.Email, order.Currency, lines, total);
    }
}
