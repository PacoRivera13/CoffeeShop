using CoffeeShop.Application.Messaging;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Messaging;

internal sealed class OutboxQuery(CoffeeShopDbContext db) : IOutboxQuery
{
    public async Task<OutboxPage> ListAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.OutboxMessages.AsNoTracking();
        if (status is not null)
        {
            var parsed = Enum.Parse<OutboxStatus>(status, ignoreCase: true);
            query = query.Where(m => m.Status == parsed);
        }

        var (items, totalCount) = await query.ToPageAsync(
            messages => messages.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id), page, pageSize, ct);

        return new OutboxPage(
            items.Select(m => new OutboxEntry(
                m.Id, m.Type, m.Status.ToString(), m.Attempts, m.NextAttemptAt, m.LastError, m.OccurredAt)).ToList(),
            page, pageSize, totalCount);
    }
}
