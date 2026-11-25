using CoffeeShop.Application.Auth;
using CoffeeShop.Application.Messaging;

namespace CoffeeShop.Api;

public static class AdminOutboxEndpoints
{
    public static void MapAdminOutboxEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/admin/outbox", ListOutbox)
            .WithTags("Admin outbox")
            .RequireAuthorization(AuthPolicies.AdminOnly)
            .WithName("ListOutboxMessages")
            .WithSummary("Lists outbox messages newest first, optionally filtered by status (Pending, Done or Failed). Requires the Admin role.")
            .Produces<OutboxPageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<IResult> ListOutbox(
        IOutboxQuery outbox, CancellationToken ct, string? status = null, int page = 1, int pageSize = 20)
    {
        var errors = new Dictionary<string, string[]>();
        var normalized = status is null
            ? null
            : OutboxConstraints.Statuses.FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        if (status is not null && normalized is null)
            errors["status"] = [$"status must be one of {string.Join(", ", OutboxConstraints.Statuses)}."];
        if (page < 1 || pageSize is < 1 or > OutboxConstraints.MaxPageSize)
            errors["page"] = [$"page must be 1 or more and pageSize between 1 and {OutboxConstraints.MaxPageSize}."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await outbox.ListAsync(normalized, page, pageSize, ct);
        return Results.Ok(new OutboxPageResponse(
            result.Items.Select(m => new OutboxMessageResponse(
                m.Id, m.Type, m.Status, m.Attempts, m.NextAttemptAt, m.LastError, m.OccurredAt)).ToList(),
            result.Page, result.PageSize, result.TotalCount));
    }
}

public sealed record OutboxMessageResponse(
    Guid Id, string Type, string Status, int Attempts, DateTimeOffset NextAttemptAt, string? LastError, DateTimeOffset OccurredAt);

public sealed record OutboxPageResponse(IReadOnlyList<OutboxMessageResponse> Items, int Page, int PageSize, int TotalCount);
