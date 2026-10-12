using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Infrastructure;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public int FirstItem => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
}

public static class PagingExtensions
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public static int NormalizePage(int? page) => page is null or < 1 ? 1 : page.Value;

    public static int NormalizePageSize(int? size) =>
        size is null or < 1 ? DefaultPageSize : Math.Min(size.Value, MaxPageSize);

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int? page, int? pageSize)
    {
        if (pageSize == int.MaxValue)
        {
            // Export: every matching row on a single page.
            var all = await query.ToListAsync();
            return new PagedResult<T> { Items = all, Page = 1, PageSize = Math.Max(all.Count, 1), TotalCount = all.Count };
        }

        var size = NormalizePageSize(pageSize);
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        var current = Math.Min(NormalizePage(page), totalPages);
        var items = await query.Skip((current - 1) * size).Take(size).ToListAsync();
        return new PagedResult<T> { Items = items, Page = current, PageSize = size, TotalCount = total };
    }

    /// <summary>Pages an in-memory list (used for aggregated report rows).</summary>
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int? page, int? pageSize)
    {
        var list = source as IList<T> ?? source.ToList();
        var size = NormalizePageSize(pageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)size));
        var current = Math.Min(NormalizePage(page), totalPages);
        return new PagedResult<T>
        {
            Items = list.Skip((current - 1) * size).Take(size).ToList(),
            Page = current,
            PageSize = size,
            TotalCount = list.Count
        };
    }
}
