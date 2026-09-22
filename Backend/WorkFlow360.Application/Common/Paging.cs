using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace WorkFlow360.Application.Common;

/// <summary>Base class for list endpoints; bound from the query string.</summary>
public class PagedQuery
{
    private const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }

    public bool Descending => string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

    public string? SearchTerm => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PagedQuery paging, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, paging.Page, paging.PageSize, totalCount);
    }

    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> query, Expression<Func<T, TKey>> keySelector, bool descending) =>
        descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> query, Expression<Func<T, TKey>> keySelector, bool descending) =>
        descending ? query.ThenByDescending(keySelector) : query.ThenBy(keySelector);
}
