using CoffeeShop.Application.Invoicing;
using CoffeeShop.Application.Messaging;
using CoffeeShop.Application.Storage;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoffeeShop.Infrastructure.Invoicing;

/// <summary>
/// Generates the PDF invoice of a paid order. It runs outside a transaction so no database locks are held while
/// uploading; idempotency comes from the deterministic storage key and the unique OrderId on Invoice.
/// </summary>
public sealed class InvoiceHandler(
    CoffeeShopDbContext db,
    IFileStorage storage,
    InvoicePdfRenderer renderer,
    TimeProvider clock,
    ILogger<InvoiceHandler> logger) : IMessageHandler<OrderPaid>, IRunsOutsideTransaction
{
    public const string ContentType = "application/pdf";

    public async Task HandleAsync(OrderPaid message, MessageContext context, CancellationToken ct)
    {
        if (await db.Invoices.AnyAsync(i => i.OrderId == message.OrderId, ct))
            return;

        var order = await db.Orders.AsNoTracking().Include(o => o.Lines).SingleAsync(o => o.Id == message.OrderId, ct);
        if (order.Status == OrderStatus.Cancelled)
        {
            // Paid and then cancelled before this event was delivered: there is nothing left to receipt.
            logger.LogInformation("Order {OrderId} was cancelled after payment; no invoice generated", order.Id);
            return;
        }

        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == order.CustomerId, ct);
        var skuIds = order.Lines.Select(l => l.SkuId).ToList();
        var skus = await db.Skus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .Join(db.Products, s => s.ProductId, p => p.Id, (s, p) => new { s.Id, p.Name, s.Grind, s.SizeG })
            .ToDictionaryAsync(x => x.Id, x => new InvoiceSku(x.Name, x.Grind, x.SizeG), ct);

        // The number is drawn before rendering because it is printed on the PDF; a failed attempt leaves a gap.
        var generatedAt = clock.GetUtcNow();
        var number = Invoice.FormatNumber(await NextSequenceValueAsync(ct), generatedAt);
        var key = Invoice.StorageKeyFor(order.Id);

        await UploadAsync(InvoiceModelBuilder.Build(order, customer, skus, number, generatedAt), key, ct);

        db.Invoices.Add(new Invoice(order.Id, number, key, order.Total, order.Currency, generatedAt));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent delivery inserted first and its upload had already finished. Re-upload with its number so
            // the stored PDF matches the row, whichever upload landed last.
            db.ChangeTracker.Clear();
            var winner = await db.Invoices.AsNoTracking().SingleAsync(i => i.OrderId == order.Id, ct);
            logger.LogInformation("Invoice {InvoiceNumber} for order {OrderId} was created concurrently; re-uploading it",
                winner.InvoiceNumber, order.Id);
            await UploadAsync(InvoiceModelBuilder.Build(order, customer, skus, winner.InvoiceNumber, winner.GeneratedAt), key, ct);
        }
    }

    private async Task UploadAsync(InvoiceDocument invoice, string key, CancellationToken ct)
    {
        using var pdf = new MemoryStream(renderer.Render(invoice));
        await storage.PutAsync(key, pdf, ContentType, ct);
    }

    // Materialized without composition: SQL Server does not allow NEXT VALUE FOR inside a derived table.
    private async Task<long> NextSequenceValueAsync(CancellationToken ct) =>
        (await db.Database
            .SqlQueryRaw<long>($"SELECT NEXT VALUE FOR [{CoffeeShopDbContext.InvoiceNumberSequence}] AS [Value]")
            .ToListAsync(ct))
        .Single();
}
