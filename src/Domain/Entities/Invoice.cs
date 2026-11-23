namespace CoffeeShop.Domain.Entities;

public class Invoice
{
    private Invoice() { }

    public Invoice(int orderId, string invoiceNumber, string storageKey, decimal totalSnapshot, string currency, DateTimeOffset generatedAt)
    {
        OrderId = orderId;
        InvoiceNumber = invoiceNumber;
        StorageKey = storageKey;
        TotalSnapshot = totalSnapshot;
        Currency = currency;
        GeneratedAt = generatedAt;
    }

    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public string InvoiceNumber { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public decimal TotalSnapshot { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>INV-{year}-{6-digit sequence}, with the year taken from the generation date in UTC.</summary>
    public static string FormatNumber(long sequence, DateTimeOffset generatedAt) =>
        $"INV-{generatedAt.UtcDateTime.Year}-{sequence:D6}";

    /// <summary>Deterministic per order, so a retried upload overwrites the same object.</summary>
    public static string StorageKeyFor(int orderId) => $"invoices/{orderId}.pdf";
}
