using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Persistence;

internal static class Paging
{
    /// <summary>
    /// Counts the query and fetches one 1-based page in the given order. The skip is computed in long, so a huge
    /// page number returns an empty page instead of overflowing into a negative OFFSET.
    /// </summary>
    public static async Task<(List<T> Items, int TotalCount)> ToPageAsync<T>(
        this IQueryable<T> query, Func<IQueryable<T>, IQueryable<T>> order, int page, int pageSize, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);

        var skip = (long)(page - 1) * pageSize;
        if (skip >= totalCount)
            return ([], totalCount);

        var items = await order(query).Skip((int)skip).Take(pageSize).ToListAsync(ct);
        return (items, totalCount);
    }
}
