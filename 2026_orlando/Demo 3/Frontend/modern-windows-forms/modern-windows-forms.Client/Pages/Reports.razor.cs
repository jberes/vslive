using System.Globalization;
using System.Text;
using ApexCharts;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Reports;
using modern_windows_forms.Client.Services;
using Format = modern_windows_forms.Client.Shared.Format;

namespace modern_windows_forms.Client.Pages;

public partial class Reports
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The nine desktop report windows, folded into one hub.</summary>
    private static readonly IReadOnlyList<ReportDefinition> Definitions =
    [
        new("year", "Sales by year", "Sales by Year window", "Yearly revenue, growth and a month-by-month comparison across every year with data.", "Line (month × year)", "/api/reports/sales-by-year"),
        new("quarter", "Sales by quarter", "Sales by Quarter window", "Quarter KPIs with a category × quarter breakdown.", "Grouped bar (category × quarter)", "/api/reports/sales-by-quarter?year="),
        new("category", "Sales by category", "Sales by Category window", "Category totals with the products that make them up.", "Donut + product bars", "/api/reports/sales-by-category?year="),
        new("monthly", "Monthly sales", "Customer Dashboard trend", "Revenue trend for the last 24 months.", "Area", "/api/reports/monthly-sales?months=24"),
        new("qorders", "Quarterly orders", "Quarterly Orders window", "Per-customer Q1–Q4 order totals.", "Grid with sparklines", "/api/reports/quarterly-orders?year="),
        new("psales", "Product sales", "Product Sales window", "Per-product Q1–Q4 revenue.", "Grid + bar", "/api/reports/product-sales?year="),
        new("topc", "Top customers", "Customer Dashboard", "Ranked customers by revenue for a year.", "Horizontal bar", "/api/reports/top-customers?year=&top="),
        new("topp", "Top products", "Product Dashboard", "Ranked products by revenue for a year.", "Horizontal bar", "/api/reports/top-products?year=&top="),
        new("country", "By country & employee", "Order Dashboard", "Revenue split by ship country and sales employee.", "Map + bar", "/api/reports/sales-by-country, /sales-by-employee"),
    ];

    /// <summary>Newest year gets chart-1; older years walk the design-system palette.</summary>
    private static readonly string[] YearColors = ["#008ffb", "#00e396", "#feb019", "#775dd0", "#ff4560"];

    [Parameter] public string? Report { get; set; }

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private ReportDefinition current = Definitions[0];
    private bool loading = true;
    private ProblemDetails? loadError;
    private string view = "chart";
    private string selectedYear = string.Empty;
    private readonly HashSet<int> hiddenYears = [];

    private SalesByYear? data;
    private DashboardSummary? summary;
    private int priorYtdOrders;
    private List<int> years = [];

    private List<ReportKpi> kpis = [];
    private List<YearSeries> series = [];
    private List<YearRowVm> yearRows = [];
    private Dictionary<int, YearRowVm> yearRowsById = [];
    private List<YearMonthsVm> monthRows = [];
    private ApexChart<MonthValue>? lineChart;

    private bool IsYear => current.Key == "year";
    private int AsOfYear => summary?.AsOf.Year ?? DateTime.Today.Year;
    private int FocusYear => int.TryParse(selectedYear, NumberStyles.Integer, Us, out var y) ? y : AsOfYear;

    protected override async Task OnParametersSetAsync()
    {
        current = Definitions.FirstOrDefault(d => string.Equals(d.Key, Report, StringComparison.OrdinalIgnoreCase)) ?? Definitions[0];
        if (data is null && loadError is null) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        loading = true;
        loadError = null;
        try
        {
            var dataTask = Api.GetAsync<SalesByYear>("api/reports/sales-by-year");
            var summaryTask = Api.GetAsync<DashboardSummary>("api/dashboard/summary");
            var yearsTask = Api.GetAsync<List<int>>("api/reports/years");
            await Task.WhenAll(dataTask, summaryTask, yearsTask);
            data = dataTask.Result;
            summary = summaryTask.Result;
            years = [.. yearsTask.Result.OrderByDescending(y => y)];
            if (string.IsNullOrEmpty(selectedYear)) selectedYear = (years.FirstOrDefault(AsOfYear)).ToString(Us);

            var asOf = summary.AsOf.Date;
            var priorStart = new DateTime(asOf.Year - 1, 1, 1).ToString("yyyy-MM-dd", Us);
            var priorEnd = asOf.AddYears(-1).ToString("yyyy-MM-dd", Us);
            priorYtdOrders = (await Api.GetAsync<PagedResult<System.Text.Json.JsonElement>>(
                $"api/orders?filter=orderDate:ge:{priorStart}&filter=orderDate:lt:{priorEnd}&pageSize=1")).TotalCount;

            Build();
        }
        catch (ApiException ex)
        {
            loadError = ex.Problem;
        }
        finally
        {
            loading = false;
        }
    }

    private void Build()
    {
        if (data is null || summary is null) return;
        BuildKpis();
        BuildSeries();
        BuildTables();
    }

    // ---------- KPIs ----------

    private void BuildKpis()
    {
        var byYear = data!.Years.ToDictionary(y => y.Year);
        var focus = FocusYear;
        byYear.TryGetValue(focus, out var cur);
        byYear.TryGetValue(focus - 1, out var prev);
        var s = summary!.Sales;

        if (focus == AsOfYear)
        {
            var aovPriorYtd = priorYtdOrders == 0 ? 0 : s.SalesLastYearToDate / priorYtdOrders;
            kpis =
            [
                new($"Revenue {focus} YTD", Format.Compact(s.SalesYearToDate), $"vs {Format.Compact(s.SalesLastYearToDate)} same period {focus - 1}",
                    Delta(s.SalesYearToDate, s.SalesLastYearToDate), Tone(s.SalesYearToDate, s.SalesLastYearToDate)),
                new("Orders", Format.Number(s.OrdersYearToDate), $"vs {Format.Number(priorYtdOrders)} same period {focus - 1}",
                    Delta(s.OrdersYearToDate, priorYtdOrders), Tone(s.OrdersYearToDate, priorYtdOrders)),
                new("Avg order value", Format.Money0(s.AverageOrderValue),
                    prev is null ? "No prior year" : $"vs {Format.Money0(prev.AverageOrderValue)} for all of {prev.Year}",
                    prev is null ? "—" : Delta(s.AverageOrderValue, prev.AverageOrderValue),
                    prev is null ? KpiTone.Neutral : Tone(s.AverageOrderValue, prev.AverageOrderValue)),
            ];
            if (prev is not null) kpis.Add(FullYearKpi(prev));
        }
        else if (cur is not null)
        {
            kpis =
            [
                FullYearKpi(cur),
                new("Orders", Format.Number(cur.Orders), prev is null ? "First year with data" : $"vs {Format.Number(prev.Orders)} in {prev.Year}",
                    prev is null ? "—" : Delta(cur.Orders, prev.Orders), prev is null ? KpiTone.Neutral : Tone(cur.Orders, prev.Orders)),
                new("Avg order value", Format.Money0(cur.AverageOrderValue), prev is null ? "First year with data" : $"vs {Format.Money0(prev.AverageOrderValue)} in {prev.Year}",
                    prev is null ? "—" : Delta(cur.AverageOrderValue, prev.AverageOrderValue), prev is null ? KpiTone.Neutral : Tone(cur.AverageOrderValue, prev.AverageOrderValue)),
                new("Customers", Format.Number(cur.Customers), prev is null ? "First year with data" : $"vs {Format.Number(prev.Customers)} in {prev.Year}",
                    prev is null ? "—" : Delta(cur.Customers, prev.Customers), prev is null ? KpiTone.Neutral : Tone(cur.Customers, prev.Customers)),
            ];
        }
        else
        {
            kpis = [];
        }
    }

    /// <summary>Full-year revenue card, calling out the best year and its peak month.</summary>
    private ReportKpi FullYearKpi(YearSales y)
    {
        var best = data!.Years.Where(x => x.Year < AsOfYear).OrderByDescending(x => x.Sales).FirstOrDefault();
        var values = data.MonthlyByYear.Series.FirstOrDefault(sr => sr.Name == y.Year.ToString(Us))?.Values ?? [];
        var peakIndex = -1;
        decimal peak = 0;
        for (var i = 0; i < values.Count; i++)
            if (values[i] is { } v && v > peak) { peak = v; peakIndex = i; }
        var peakText = peakIndex >= 0 ? $"{data.MonthlyByYear.Labels[peakIndex]} {Format.Compact(peak)} peak" : "";
        var lead = best?.Year == y.Year ? "Best full year" : "Full year";
        var delta = y.GrowthPercent is { } g ? Format.Signed(g) : "—";
        var tone = y.GrowthPercent is null or 0 ? KpiTone.Neutral : y.GrowthPercent > 0 ? KpiTone.Up : KpiTone.Down;
        return new($"Revenue {y.Year}", Format.Compact(y.Sales), string.IsNullOrEmpty(peakText) ? lead : $"{lead} · {peakText}", delta, tone);
    }

    private static string Delta(decimal current, decimal prior) => prior == 0 ? "—" : Format.Signed((current - prior) / prior * 100m);

    private static KpiTone Tone(decimal current, decimal prior) =>
        prior == 0 || current == prior ? KpiTone.Neutral : current > prior ? KpiTone.Up : KpiTone.Down;

    private static string DeltaClass(KpiTone tone) => tone switch
    {
        KpiTone.Up => "delta delta--up",
        KpiTone.Down => "delta delta--down",
        _ => "delta"
    };

    // ---------- Chart ----------

    private void BuildSeries()
    {
        var labels = data!.MonthlyByYear.Labels;
        var ordered = data.MonthlyByYear.Series
            .Select(sr => (Year: int.Parse(sr.Name, Us), Values: sr.Values))
            .OrderByDescending(x => x.Year)
            .ToList();
        series = ordered.Select((x, i) => new YearSeries(
                x.Year,
                YearColors[Math.Min(i, YearColors.Length - 1)],
                x.Year == AsOfYear ? 3 : 2,
                x.Values.Select((v, m) => (v, m)).Where(p => p.v.HasValue && !(x.Year == AsOfYear && p.v == 0))
                        .Select(p => new MonthValue(labels[p.m], Math.Round(p.v!.Value, 2))).ToList()))
            .OrderBy(x => x.Year)
            .ToList();
    }

    private IEnumerable<YearSeries> VisibleSeries => series.Where(s => !hiddenYears.Contains(s.Year));

    private void ToggleYear(int year)
    {
        if (!hiddenYears.Remove(year)) hiddenYears.Add(year);
    }

    private string LegendClass(int year) => hiddenYears.Contains(year) ? "pill year-pill year-pill--off" : "pill year-pill pill--on";

    private string ChartKey => string.Join("-", VisibleSeries.Select(s => s.Year));

    private ApexChartOptions<MonthValue> LineOptions => new()
    {
        Stroke = new Stroke { Curve = Curve.Straight, LineCap = LineCap.Round },
        Grid = new Grid { BorderColor = "#f0f0f0", Xaxis = new GridXAxis { Lines = new Lines { Show = false } } },
        Xaxis = new XAxis
        {
            Categories = data?.MonthlyByYear.Labels.ToList(),
            AxisBorder = new AxisBorder { Color = "#e5e7eb" },
            AxisTicks = new AxisTicks { Show = false },
            Labels = new XAxisLabels { Style = new AxisLabelStyle { Colors = "#6b7280", FontSize = "11px" } },
            Tooltip = new XAxisTooltip { Enabled = false }
        },
        Yaxis = [new YAxis { Min = 0, TickAmount = 4, Labels = new YAxisLabels { Style = new AxisLabelStyle { Colors = "#6b7280", FontSize = "11px" } } }],
        Markers = new Markers { Size = 0, Hover = new MarkersHover { Size = 5 }, StrokeColors = "#ffffff", StrokeWidth = 2 },
        Tooltip = new Tooltip { Shared = true, Intersect = false },
        Legend = new Legend { Show = false }
    };

    private ApexChartOptions<YearRowVm> YearBarOptions => new()
    {
        Colors = series.Select(s => s.Color).ToList(),
        PlotOptions = new PlotOptions
        {
            Bar = new PlotOptionsBar
            {
                Distributed = true, ColumnWidth = "55%", BorderRadius = 4,
                DataLabels = new PlotOptionsBarDataLabels { Position = BarDataLabelPosition.Top }
            }
        },
        Fill = new Fill { Opacity = 0.85 },
        DataLabels = new DataLabels
        {
            Enabled = true,
            OffsetY = -20,
            Formatter = "function (v) { return '$' + (v / 1000000).toFixed(2) + 'M'; }",
            Style = new DataLabelsStyle { Colors = ["#1a1a2e"], FontSize = "12px", FontWeight = "600" }
        },
        Grid = new Grid { BorderColor = "#f0f0f0", Xaxis = new GridXAxis { Lines = new Lines { Show = false } } },
        Xaxis = new XAxis
        {
            AxisBorder = new AxisBorder { Color = "#e5e7eb" },
            AxisTicks = new AxisTicks { Show = false },
            Labels = new XAxisLabels { Style = new AxisLabelStyle { Colors = "#4b5563", FontSize = "12px" } }
        },
        Yaxis = [new YAxis { Show = false, Min = 0 }],
        Tooltip = new Tooltip { Enabled = false },
        Legend = new Legend { Show = false }
    };

    private static string MoneyAxis(decimal v) => Format.Compact(v);

    // ---------- Tables ----------

    private void BuildTables()
    {
        var s = summary!.Sales;
        yearRows = data!.Years.OrderBy(y => y.Year).Select(y =>
        {
            // The current year compares year-to-date with the same period last year (the API's figure compares with all of last year).
            var growth = y.Year == AsOfYear ? s.GrowthPercent : y.GrowthPercent;
            return new YearRowVm
            {
                Year = y.Year,
                YearLabel = y.Year == AsOfYear ? $"{y.Year} YTD" : y.Year.ToString(Us),
                Sales = y.Sales,
                Revenue = Format.Money0(y.Sales),
                Growth = growth is { } g ? Format.Signed(g) : "—",
                GrowthTone = growth is null or 0 ? "neutral" : growth > 0 ? "up" : "down",
                Orders = Format.Number(y.Orders),
                Customers = Format.Number(y.Customers),
                AverageOrder = Format.Money(y.AverageOrderValue)
            };
        }).ToList();
        yearRowsById = yearRows.ToDictionary(r => r.Year);

        var labels = data.MonthlyByYear.Labels;
        monthRows = data.MonthlyByYear.Series.Select(sr =>
        {
            var year = int.Parse(sr.Name, Us);
            string Cell(int m) => m < sr.Values.Count && sr.Values[m] is { } v && !(year == AsOfYear && v == 0) ? Format.Compact(v) : "—";
            return new YearMonthsVm
            {
                Year = year,
                YearLabel = year == AsOfYear ? $"{year} YTD" : year.ToString(Us),
                Jan = Cell(0), Feb = Cell(1), Mar = Cell(2), Apr = Cell(3), May = Cell(4), Jun = Cell(5),
                Jul = Cell(6), Aug = Cell(7), Sep = Cell(8), Oct = Cell(9), Nov = Cell(10), Dec = Cell(11),
                Total = Format.Compact(sr.Values.Sum(v => v ?? 0))
            };
        }).OrderByDescending(r => r.Year).ToList();
        _ = labels;
    }

    private string GrowthClass(IgbCellTemplateContext ctx) =>
        int.TryParse(ctx.Cell?.Id?.RowID, NumberStyles.Integer, Us, out var id) && yearRowsById.TryGetValue(id, out var row)
            ? $"num growth growth--{row.GrowthTone}" : "num growth";

    private string ExportHref
    {
        get
        {
            var csv = new StringBuilder("Year,Revenue,Growth %,Orders,Customers,Avg order\n");
            foreach (var y in data?.Years ?? [])
                csv.Append(CultureInfo.InvariantCulture, $"{y.Year},{y.Sales},{y.GrowthPercent},{y.Orders},{y.Customers},{y.AverageOrderValue}\n");
            return "data:text/csv;charset=utf-8," + Uri.EscapeDataString(csv.ToString());
        }
    }

    // ---------- Interaction ----------

    private void Pick(ReportDefinition def) => Nav.NavigateTo(def.Key == "year" ? "reports" : $"reports/{def.Key}");

    private void OpenSalesByYear() => Nav.NavigateTo("reports");

    private void SetView(IgbComponentValueChangedEventArgs e)
    {
        if (e.Detail is "chart" or "table") view = e.Detail;
    }

    private void OnYearChanged()
    {
        if (data is not null) BuildKpis();
    }
}
