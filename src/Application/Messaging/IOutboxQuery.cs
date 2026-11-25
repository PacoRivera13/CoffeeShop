namespace CoffeeShop.Application.Messaging;

public static class OutboxConstraints
{
    public const int MaxPageSize = 100;

    public static readonly IReadOnlyList<string> Statuses = ["Pending", "Done", "Failed"];
}

public sealed record OutboxEntry(
    Guid Id, string Type, string Status, int Attempts, DateTimeOffset NextAttemptAt, string? LastError, DateTimeOffset OccurredAt);

public sealed record OutboxPage(IReadOnlyList<OutboxEntry> Items, int Page, int PageSize, int TotalCount);

/// <summary>Read-only view of the outbox for operators.</summary>
public interface IOutboxQuery
{
    /// <summary>Lists messages newest first, optionally only those in one status (one of <see cref="OutboxConstraints.Statuses"/>).</summary>
    Task<OutboxPage> ListAsync(string? status, int page, int pageSize, CancellationToken ct);
}
