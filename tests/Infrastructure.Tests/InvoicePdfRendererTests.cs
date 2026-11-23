using System.Text;
using CoffeeShop.Application.Invoicing;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Invoicing;

namespace CoffeeShop.Infrastructure.Tests;

public class InvoicePdfRendererTests
{
    [Fact]
    public void Renders_a_pdf_document()
    {
        var invoice = new InvoiceDocument(
            "INV-2026-000001",
            new DateTimeOffset(2026, 11, 21, 9, 30, 0, TimeSpan.Zero),
            12,
            "Alice",
            "alice@example.test",
            "EUR",
            [
                new InvoiceLine("Yirgacheffe", Grind.WholeBean, 500, 2, 12.50m, 25.00m),
                new InvoiceLine("Huila", Grind.Ground, 1000, 3, 18.00m, 54.00m),
            ],
            79.00m);

        var pdf = new InvoicePdfRenderer().Render(invoice);

        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
