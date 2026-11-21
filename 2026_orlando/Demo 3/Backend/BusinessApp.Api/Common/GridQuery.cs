using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Common;

/// <summary>
/// Standard grid query string shared by every list endpoint.
/// <list type="bullet">
/// <item><c>page</c> (1-based, default 1) and <c>pageSize</c> (default 25, max 1000).</item>
/// <item><c>sort</c>: comma separated fields, prefix with <c>-</c> or suffix with <c>:desc</c> for descending. e.g. <c>sort=country,-companyName</c>.</item>
/// <item><c>search</c>: free text, matched against endpoint-specific text columns.</item>
/// <item><c>filter</c>: repeatable <c>field:op:value</c>. Ops: eq, ne, contains, startswith, endswith, gt, ge, lt, le, in (values separated by |), null, notnull.</item>
/// </list>
/// </summary>
public sealed class GridQuery
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Sort { get; set; }
    public string? Search { get; set; }
    public string[]? Filter { get; set; }

    public const int MaxPageSize = 1000;
    public int PageNumber => Math.Max(1, Page ?? 1);
    public int Size => Math.Clamp(PageSize ?? 25, 1, MaxPageSize);
}

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class GridQueryException(string message) : Exception(message);

public static class GridQueryExtensions
{
    /// <summary>Applies search, filters, sorting and paging to a projected (DTO) query.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        GridQuery grid,
        Func<string, Expression<Func<T, bool>>>? search = null,
        string? defaultSort = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(grid.Search) && search is not null)
            query = query.Where(search(grid.Search.Trim().ToLower()));

        foreach (var f in grid.Filter ?? []) query = query.ApplyFilter(f);

        var total = await query.CountAsync(ct);
        query = query.ApplySort(string.IsNullOrWhiteSpace(grid.Sort) ? defaultSort : grid.Sort);

        var items = await query.Skip((grid.PageNumber - 1) * grid.Size).Take(grid.Size).ToListAsync(ct);
        return new PagedResult<T> { Items = items, TotalCount = total, Page = grid.PageNumber, PageSize = grid.Size };
    }

    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return query;
        var first = true;
        foreach (var raw in sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var desc = raw.StartsWith('-') || raw.EndsWith(":desc", StringComparison.OrdinalIgnoreCase);
            var name = raw.TrimStart('-', '+').Split(':')[0];
            var prop = FindProperty<T>(name);

            var param = Expression.Parameter(typeof(T), "x");
            var body = Expression.Property(param, prop);
            var lambda = Expression.Lambda(body, param);
            var method = (first, desc) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending),
            };
            query = query.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method, [typeof(T), prop.PropertyType], query.Expression, Expression.Quote(lambda)));
            first = false;
        }
        return query;
    }

    public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> query, string filter)
    {
        var parts = filter.Split(':', 3);
        if (parts.Length < 2) throw new GridQueryException($"Invalid filter '{filter}'. Expected field:op:value.");
        var prop = FindProperty<T>(parts[0]);
        var op = parts[1].ToLowerInvariant();
        var value = parts.Length == 3 ? parts[2] : null;

        var param = Expression.Parameter(typeof(T), "x");
        Expression member = Expression.Property(param, prop);
        var type = prop.PropertyType;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        Expression body;
        if (op is "null" or "notnull")
        {
            if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
                throw new GridQueryException($"Field '{parts[0]}' is not nullable.");
            var nullConst = Expression.Constant(null, type);
            body = op == "null" ? Expression.Equal(member, nullConst) : Expression.NotEqual(member, nullConst);
        }
        else if (value is null)
        {
            throw new GridQueryException($"Filter '{filter}' requires a value.");
        }
        else if (underlying == typeof(string))
        {
            var lower = Expression.Call(Expression.Coalesce(member, Expression.Constant("")), nameof(string.ToLower), Type.EmptyTypes);
            Expression Str(string method, string v) => Expression.Call(lower, typeof(string).GetMethod(method, [typeof(string)])!, Expression.Constant(v.ToLower()));
            body = op switch
            {
                "eq" => Expression.Equal(lower, Expression.Constant(value.ToLower())),
                "ne" => Expression.NotEqual(lower, Expression.Constant(value.ToLower())),
                "contains" => Str(nameof(string.Contains), value),
                "startswith" => Str(nameof(string.StartsWith), value),
                "endswith" => Str(nameof(string.EndsWith), value),
                "in" => value.Split('|').Select(v => (Expression)Expression.Equal(lower, Expression.Constant(v.ToLower()))).Aggregate(Expression.OrElse),
                "gt" => Expression.GreaterThan(Expression.Call(typeof(string), nameof(string.Compare), null, lower, Expression.Constant(value.ToLower())), Expression.Constant(0)),
                "lt" => Expression.LessThan(Expression.Call(typeof(string), nameof(string.Compare), null, lower, Expression.Constant(value.ToLower())), Expression.Constant(0)),
                _ => throw new GridQueryException($"Operator '{op}' is not supported for text field '{parts[0]}'."),
            };
        }
        else
        {
            Expression Const(string v) => Expression.Constant(ConvertValue(v, underlying, parts[0]), type);
            body = op switch
            {
                "eq" => Expression.Equal(member, Const(value)),
                "ne" => Expression.NotEqual(member, Const(value)),
                "gt" => Expression.GreaterThan(member, Const(value)),
                "ge" => Expression.GreaterThanOrEqual(member, Const(value)),
                "lt" => Expression.LessThan(member, Const(value)),
                "le" => Expression.LessThanOrEqual(member, Const(value)),
                "in" => value.Split('|').Select(v => (Expression)Expression.Equal(member, Const(v))).Aggregate(Expression.OrElse),
                _ => throw new GridQueryException($"Operator '{op}' is not supported for field '{parts[0]}'."),
            };
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(body, param));
    }

    private static object ConvertValue(string value, Type type, string field)
    {
        try
        {
            if (type.IsEnum) return Enum.Parse(type, value, ignoreCase: true);
            if (type == typeof(bool)) return bool.Parse(value);
            if (type == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
            if (type == typeof(DateOnly)) return DateOnly.Parse(value, CultureInfo.InvariantCulture);
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidCastException or OverflowException)
        {
            throw new GridQueryException($"Value '{value}' is not valid for field '{field}' ({type.Name}).");
        }
    }

    private static PropertyInfo FindProperty<T>(string name) =>
        typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is { CanWrite: true } p
            ? p
            : throw new GridQueryException($"Unknown field '{name}'. Valid fields: {string.Join(", ", typeof(T).GetProperties().Where(p => p.CanWrite).Select(p => JsonName(p.Name)))}.");

    private static string JsonName(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
