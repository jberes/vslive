using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Infrastructure;

/// <summary>Standard paged response envelope.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Common query-string paging/sorting parameters.</summary>
public class PageQuery
{
    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;
    /// <summary>Rows per page (max 500).</summary>
    public int PageSize { get; set; } = 50;
    /// <summary>Sort expression: comma separated property paths, prefix with '-' for descending. e.g. "-priority,scheduledStart" or "customer.name".</summary>
    public string? Sort { get; set; }
    /// <summary>Free-text search.</summary>
    public string? Search { get; set; }

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, 500);
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<TOut>> ToPagedAsync<T, TOut>(this IQueryable<T> query, PageQuery q,
        Expression<Func<T, TOut>> projection, string defaultSort, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.ApplySort(q.Sort, defaultSort)
            .Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize)
            .Select(projection).ToListAsync(ct);
        return new PagedResult<TOut>(items, total, q.SafePage, q.SafePageSize);
    }

    /// <summary>Applies a dynamic sort over entity property paths (case-insensitive). Unknown fields are ignored.</summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sort, string defaultSort)
    {
        var spec = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort;
        var first = true;
        foreach (var raw in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var desc = raw.StartsWith('-');
            var path = raw.TrimStart('-', '+');
            var param = Expression.Parameter(typeof(T), "x");
            Expression body = param;
            var ok = true;
            foreach (var part in path.Split('.'))
            {
                var prop = body.Type.GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) { ok = false; break; }
                body = Expression.Property(body, prop);
            }
            if (!ok) continue;
            var lambda = Expression.Lambda(body, param);
            var method = first ? (desc ? "OrderByDescending" : "OrderBy") : (desc ? "ThenByDescending" : "ThenBy");
            query = (IQueryable<T>)typeof(Queryable).GetMethods()
                .First(m => m.Name == method && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), body.Type)
                .Invoke(null, [query, lambda])!;
            first = false;
        }
        return first && spec != defaultSort ? query.ApplySort(defaultSort, defaultSort) : query;
    }
}
