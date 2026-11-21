namespace BusinessApp.Api.Common;

/// <summary>Single-series chart point (pie, bar, donut).</summary>
public sealed record NameValue(string Name, double Value);

/// <summary>Multi-series chart data: one label per X position and one array of values per series.</summary>
public sealed record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series)
{
    public static ChartData From<TRow>(IReadOnlyList<string> labels, IEnumerable<string> seriesNames,
        IEnumerable<TRow> rows, Func<TRow, string> label, Func<TRow, string> series, Func<TRow, double> value)
    {
        var lookup = rows.GroupBy(r => (label(r), series(r))).ToDictionary(g => g.Key, g => g.Sum(value));
        return new ChartData(labels, seriesNames
            .Select(s => new ChartSeries(s, labels.Select(l => Money.Round(lookup.GetValueOrDefault((l, s)))).ToList()))
            .ToList());
    }
}

public sealed record ChartSeries(string Name, IReadOnlyList<double> Values);

public sealed record LookupItem(int Id, string Name);

public static class Money
{
    public static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    public static double Round(double? value) => Round(value ?? 0);
}

public static class Avatars
{
    public static string Url(string kind, int id) => $"/api/avatars/{kind}/{id}.svg";
}
