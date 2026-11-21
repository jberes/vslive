namespace modern_windows_forms.Client.Models;

/// <summary>Shape of every grid-query list endpoint: <c>{ items, totalCount, page, pageSize, totalPages }</c>.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

/// <summary>Multi-series chart payload: <c>{ labels: [...], series: [{ name, values: [...] }] }</c>.</summary>
public sealed record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series);
public sealed record ChartSeries(string Name, IReadOnlyList<decimal?> Values);

/// <summary>Pie / bar payload item: <c>{ name, value }</c>.</summary>
public sealed record NameValue(string Name, decimal Value);

/// <summary>RFC 7807 problem details returned by the API (400 validation, 404, 409).</summary>
public sealed class ProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>First validation message for a field, matching case-insensitively (API keys are PascalCase).</summary>
    public string? ErrorFor(string field) =>
        Errors?.FirstOrDefault(e => string.Equals(e.Key, field, StringComparison.OrdinalIgnoreCase)
                                    || e.Key.EndsWith("." + field, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
}
