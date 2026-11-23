using System.Globalization;
using CoffeeShop.Application.Invoicing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CoffeeShop.Infrastructure.Invoicing;

public sealed class InvoicePdfRenderer
{
    // The single place the QuestPDF license is chosen; it runs before any document is rendered.
    static InvoicePdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(InvoiceDocument invoice) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().Column(header =>
            {
                header.Item().Text("CoffeeShop").FontSize(20).SemiBold();
                header.Item().Text($"Invoice {invoice.InvoiceNumber}").FontSize(14);
                header.Item().Text($"Date {invoice.IssuedAt.UtcDateTime:yyyy-MM-dd}    Order {invoice.OrderId}");
            });

            page.Content().PaddingVertical(20).Column(content =>
            {
                content.Item().Text($"Bill to: {invoice.CustomerName} <{invoice.CustomerEmail}>");

                content.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        foreach (var title in new[] { "Product", "Grind", "Size", "Qty", "Unit price", "Line total" })
                            header.Cell().BorderBottom(1).PaddingVertical(4).Text(title).SemiBold();
                    });

                    foreach (var line in invoice.Lines)
                    {
                        table.Cell().PaddingVertical(3).Text(line.ProductName);
                        table.Cell().PaddingVertical(3).Text(line.Grind.ToString());
                        table.Cell().PaddingVertical(3).Text($"{line.SizeG} g");
                        table.Cell().PaddingVertical(3).Text(line.Qty.ToString(CultureInfo.InvariantCulture));
                        table.Cell().PaddingVertical(3).AlignRight().Text(Money(line.UnitPrice));
                        table.Cell().PaddingVertical(3).AlignRight().Text(Money(line.LineTotal));
                    }
                });

                content.Item().PaddingTop(12).AlignRight()
                    .Text($"Total {Money(invoice.Total)} {invoice.Currency}").FontSize(12).Bold();
            });

            page.Footer().AlignCenter().Text("Demo receipt - not fiscally valid").FontSize(8);
        })).GeneratePdf();

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
