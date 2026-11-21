using System.Globalization;
using ApexCharts;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Home;
using modern_windows_forms.Client.Services;
using modern_windows_forms.Client.Shared;
using Format = modern_windows_forms.Client.Shared.Format;

namespace modern_windows_forms.Client.Pages;

public partial class Home
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] SparkColors = ["#008ffb", "#00b0a0", "#775dd0", "#feb019"];
    private static readonly string[] OpenStages = ["New", "Contacted", "Qualified", "Proposal"];

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ILogger<Home> Logger { get; set; } = default!;

    private bool loading = true;
    private ProblemDetails? loadError;
    private string range = "30";

    private DateTime asOf = DateTime.Today;
    private DashboardSummary? summary;
    private List<MonthlySales> monthly = [];
    private SalesOverview? overview;
    private List<OrderRow> recentWindow = [];
    private int priorYtdOrders;
    private MarketingDashboard? marketing;

    private List<KpiCard> kpis30 = [];
    private List<KpiCard> kpisYtd = [];
    private List<MonthPoint> months = [];
    private decimal monthlyAverage;
    private List<NameValue> categories = [];
    private decimal categoryTotal;
    private List<RecentOrderVm> recentOrders = [];
    private Dictionary<int, RecentOrderVm> recentById = [];
    private List<AttentionItem> attention = [];
    private List<ProjectRow> projects = [];
    private List<CampaignRow> campaigns = [];

    private List<KpiCard> Kpis => range == "30" ? kpis30 : kpisYtd;

    private string Greeting => DateTime.Now.Hour switch { < 12 => "Good morning", < 18 => "Good afternoon", _ => "Good evening" };

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        loading = true;
        loadError = null;
        try
        {
            summary = await Api.GetAsync<DashboardSummary>("api/dashboard/summary");
            asOf = summary.AsOf.Date;
            var windowStart = asOf.AddDays(-60).ToString("yyyy-MM-dd", Us);
            var priorStart = new DateTime(asOf.Year - 1, 1, 1).ToString("yyyy-MM-dd", Us);
            var priorEnd = asOf.AddYears(-1).ToString("yyyy-MM-dd", Us);

            var monthlyTask = Api.GetAsync<List<MonthlySales>>("api/reports/monthly-sales?months=13");
            var overviewTask = Api.GetAsync<SalesOverview>("api/dashboard/sales-overview");
            var windowTask = Api.GetAsync<PagedResult<OrderRow>>($"api/orders?filter=orderDate:ge:{windowStart}&sort=orderDate&pageSize=1000");
            var priorTask = Api.GetAsync<PagedResult<OrderRow>>($"api/orders?filter=orderDate:ge:{priorStart}&filter=orderDate:lt:{priorEnd}&pageSize=1");
            var marketingTask = Api.GetAsync<MarketingDashboard>("api/marketing/dashboard?months=12");
            var projectsTask = Api.GetAsync<PagedResult<ProjectRow>>("api/projects?filter=status:in:Active|OnHold&sort=status,startDate&pageSize=10");
            var campaignsTask = Api.GetAsync<PagedResult<CampaignRow>>("api/campaigns?filter=status:eq:Active&sort=-actualCost&pageSize=5");

            await Task.WhenAll(monthlyTask, overviewTask, windowTask, priorTask, marketingTask, projectsTask, campaignsTask);

            monthly = [.. monthlyTask.Result];
            overview = overviewTask.Result;
            recentWindow = [.. windowTask.Result.Items];
            priorYtdOrders = priorTask.Result.TotalCount;
            marketing = marketingTask.Result;
            projects = [.. projectsTask.Result.Items];
            campaigns = [.. campaignsTask.Result.Items];

            BuildKpis();
            BuildCharts();
            BuildRecentOrders();
            attention = await BuildAttentionAsync();
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

    // ---------- KPIs ----------

    private void BuildKpis()
    {
        if (summary is null || marketing is null) return;

        // Last 30 days = the 30 full days before today; prior = the 30 days before that.
        var current = recentWindow.Where(o => o.OrderDate.Date >= asOf.AddDays(-30) && o.OrderDate.Date < asOf).ToList();
        var prior = recentWindow.Where(o => o.OrderDate.Date >= asOf.AddDays(-60) && o.OrderDate.Date < asOf.AddDays(-30)).ToList();
        decimal rev = current.Sum(o => o.Subtotal), revPrior = prior.Sum(o => o.Subtotal);
        decimal aov = current.Count == 0 ? 0 : rev / current.Count, aovPrior = prior.Count == 0 ? 0 : revPrior / prior.Count;

        // Ten 3-day buckets for the 30-day sparklines.
        var buckets = Enumerable.Range(0, 10).Select(i =>
        {
            var from = asOf.AddDays(-30 + i * 3);
            return current.Where(o => o.OrderDate.Date >= from && o.OrderDate.Date < from.AddDays(3)).ToList();
        }).ToList();

        var pipelineSpark = Spark(OpenStages.Select(s => marketing.LeadsByStatus.FirstOrDefault(x => x.Name == s)?.Value ?? 0));
        var pipeline = new KpiCard("Open pipeline", Format.Compact(marketing.Kpis.PipelineValue),
            $"{Format.Number(marketing.Kpis.OpenLeads)} open leads in {OpenStages.Length} stages",
            $"{OpenStages.Length} stages", DeltaTone.Neutral, "leads", SparkColors[3], pipelineSpark);

        kpis30 =
        [
            new("Revenue", Format.Compact(rev), $"vs {Format.Compact(revPrior)} prior 30 days", Delta(rev, revPrior), Tone(rev, revPrior),
                "reports", SparkColors[0], Spark(buckets.Select(b => b.Sum(o => o.Subtotal)))),
            new("Orders", Format.Number(current.Count), $"vs {Format.Number(prior.Count)} prior 30 days", Delta(current.Count, prior.Count), Tone(current.Count, prior.Count),
                "orders", SparkColors[1], Spark(buckets.Select(b => (decimal)b.Count))),
            new("Avg order value", Format.Money0(aov), $"vs {Format.Money0(aovPrior)} prior 30 days", Delta(aov, aovPrior), Tone(aov, aovPrior),
                "reports", SparkColors[2], Spark(buckets.Where(b => b.Count > 0).Select(b => b.Sum(o => o.Subtotal) / b.Count))),
            pipeline
        ];

        var s = summary.Sales;
        var ytdMonths = monthly.Where(m => m.Month.StartsWith(asOf.Year.ToString(Us), StringComparison.Ordinal)).ToList();
        var aovPriorYtd = priorYtdOrders == 0 ? 0 : s.SalesLastYearToDate / priorYtdOrders;
        var priorLabel = $"same period {asOf.Year - 1}";
        kpisYtd =
        [
            new("Revenue", Format.Compact(s.SalesYearToDate), $"vs {Format.Compact(s.SalesLastYearToDate)} {priorLabel}", Delta(s.SalesYearToDate, s.SalesLastYearToDate),
                Tone(s.SalesYearToDate, s.SalesLastYearToDate), "reports", SparkColors[0], Spark(ytdMonths.Select(m => m.Sales))),
            new("Orders", Format.Number(s.OrdersYearToDate), $"vs {Format.Number(priorYtdOrders)} {priorLabel}", Delta(s.OrdersYearToDate, priorYtdOrders),
                Tone(s.OrdersYearToDate, priorYtdOrders), "orders", SparkColors[1], Spark(ytdMonths.Select(m => (decimal)m.Orders))),
            new("Avg order value", Format.Money0(s.AverageOrderValue), $"vs {Format.Money0(aovPriorYtd)} {priorLabel}", Delta(s.AverageOrderValue, aovPriorYtd),
                Tone(s.AverageOrderValue, aovPriorYtd), "reports", SparkColors[2], Spark(ytdMonths.Where(m => m.Orders > 0).Select(m => m.Sales / m.Orders))),
            pipeline
        ];
    }

    private static List<SparkPoint> Spark(IEnumerable<decimal> values) =>
        values.Select((v, i) => new SparkPoint(i, Math.Round(v, 2))).ToList();

    private static string Delta(decimal current, decimal prior) =>
        prior == 0 ? "—" : Format.Signed((current - prior) / prior * 100m);

    private static DeltaTone Tone(decimal current, decimal prior) =>
        prior == 0 || current == prior ? DeltaTone.Neutral : current > prior ? DeltaTone.Up : DeltaTone.Down;

    private static string DeltaClass(DeltaTone tone) => tone switch
    {
        DeltaTone.Up => "delta delta--up",
        DeltaTone.Down => "delta delta--down",
        _ => "delta"
    };

    private void SetRange(IgbComponentValueChangedEventArgs e)
    {
        if (e.Detail is "30" or "ytd") range = e.Detail;
    }

    // ---------- Charts ----------

    private void BuildCharts()
    {
        months = monthly.Select((m, i) =>
        {
            var date = DateTime.ParseExact(m.Month, "yyyy-MM", Us);
            var isPartial = i == monthly.Count - 1 && date.Year == asOf.Year && date.Month == asOf.Month;
            return new MonthPoint(date.ToString("MMM", Us), date.ToString("MMMM yyyy", Us) + (isPartial ? " (to date)" : ""), m.Sales, m.Orders);
        }).ToList();

        var complete = monthly.Count > 1 ? monthly.Take(monthly.Count - 1).ToList() : monthly;
        monthlyAverage = complete.Count == 0 ? 0 : Math.Round(complete.Average(m => m.Sales), 0);

        categories = overview?.SalesByCategory.OrderByDescending(c => c.Value).ToList() ?? [];
        categoryTotal = categories.Sum(c => c.Value);
    }

    private string RevenueRangeLabel =>
        months.Count == 0 ? string.Empty :
        $"{FirstMonthLabel} – {LastMonthLabel} · {LastMonthName} is month to date";

    private string FirstMonthLabel => monthly.Count == 0 ? "" : DateTime.ParseExact(monthly[0].Month, "yyyy-MM", Us).ToString("MMM yyyy", Us);
    private string LastMonthLabel => monthly.Count == 0 ? "" : DateTime.ParseExact(monthly[^1].Month, "yyyy-MM", Us).ToString("MMM yyyy", Us);
    private string LastMonthName => monthly.Count == 0 ? "" : DateTime.ParseExact(monthly[^1].Month, "yyyy-MM", Us).ToString("MMMM", Us);

    private ApexChartOptions<SparkPoint> SparkOptions(string color) => new()
    {
        Chart = new Chart { Sparkline = new ChartSparkline { Enabled = true }, Animations = new Animations { Enabled = false } },
        Colors = [color],
        Stroke = new Stroke { Width = 2, Curve = Curve.Straight },
        Fill = new Fill { Type = FillType.Solid, Opacity = 0.13 },
        Tooltip = new Tooltip { Enabled = false },
        States = new ApexCharts.States { Hover = new StatesHover { Filter = new StatesFilter { Type = StatesFilterType.none } } }
    };

    private ApexChartOptions<MonthPoint> RevenueOptions => new()
    {
        Colors = ["#008ffb"],
        Stroke = new Stroke { Width = 2.5, Curve = Curve.Straight },
        Fill = new Fill
        {
            Type = FillType.Gradient,
            Gradient = new FillGradient { ShadeIntensity = 0, OpacityFrom = 0.4, OpacityTo = 0.05, Stops = [0, 100] }
        },
        Grid = new Grid { BorderColor = "#f0f0f0", Xaxis = new GridXAxis { Lines = new Lines { Show = false } } },
        Xaxis = new XAxis
        {
            AxisBorder = new AxisBorder { Color = "#e5e7eb" },
            AxisTicks = new AxisTicks { Show = false },
            Labels = new XAxisLabels { Style = new AxisLabelStyle { Colors = "#6b7280", FontSize = "11px" } },
            Tooltip = new XAxisTooltip { Enabled = false }
        },
        Yaxis = [new YAxis { Min = 0, TickAmount = 4, Labels = new YAxisLabels { Style = new AxisLabelStyle { Colors = "#6b7280", FontSize = "11px" } } }],
        Markers = new Markers { Size = 0, Hover = new MarkersHover { Size = 5 }, StrokeColors = "#ffffff", StrokeWidth = 2 },
        Annotations = new Annotations
        {
            Yaxis =
            [
                new AnnotationsYAxis
                {
                    Y = (double)monthlyAverage, BorderColor = "#6b7280", StrokeDashArray = 4,
                    Label = new Label { Text = string.Empty }
                }
            ]
        },
        Legend = new Legend { Show = false }
    };

    private ApexChartOptions<NameValue> CategoryOptions => new()
    {
        Colors = ["#008ffb"],
        PlotOptions = new PlotOptions { Bar = new PlotOptionsBar { Horizontal = true, BorderRadius = 4, BarHeight = "60%" } },
        Fill = new Fill { Opacity = 0.85 },
        Grid = new Grid { BorderColor = "#f0f0f0", Xaxis = new GridXAxis { Lines = new Lines { Show = false } }, Yaxis = new GridYAxis { Lines = new Lines { Show = false } } },
        Xaxis = new XAxis
        {
            AxisBorder = new AxisBorder { Show = false },
            AxisTicks = new AxisTicks { Show = false },
            Labels = new XAxisLabels { Show = false }
        },
        Yaxis = [new YAxis { Labels = new YAxisLabels { MaxWidth = 120, Style = new AxisLabelStyle { Colors = "#1a1a2e", FontSize = "12px" } } }],
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Show = false }
    };

    private static string MoneyK(decimal v) => Format.Compact(v);

    // ---------- Recent orders ----------

    private void BuildRecentOrders()
    {
        recentOrders = (overview?.RecentOrders ?? []).Take(6).Select(o => new RecentOrderVm
        {
            Id = o.Id,
            Customer = o.CustomerName,
            Initials = Format.Initials(o.CustomerName),
            AvatarClass = Format.AvatarClass(o.CustomerName),
            Date = Format.Date(o.OrderDate),
            ShipVia = o.ShipViaName ?? "—",
            Total = o.Subtotal,
            TotalText = Format.Money(o.Subtotal),
            Status = o.Status
        }).ToList();
        recentById = recentOrders.ToDictionary(o => o.Id);
    }

    private RecentOrderVm? RowOf(IgbCellTemplateContext ctx) =>
        int.TryParse(ctx.Cell?.Id?.RowID, NumberStyles.Integer, Us, out var id) && recentById.TryGetValue(id, out var vm) ? vm : null;

    private void OpenOrders() => Nav.NavigateTo("orders");

    // ---------- Needs attention ----------

    private async Task<List<AttentionItem>> BuildAttentionAsync()
    {
        var items = new List<AttentionItem>();
        try
        {
            var overdueTask = Api.GetAsync<PagedResult<OrderRow>>("api/orders?filter=status:eq:Overdue&sort=-requiredDate&pageSize=2");
            var outTask = Api.GetAsync<PagedResult<ProductRow>>("api/products?filter=unitsInStock:eq:0&filter=discontinued:eq:false&pageSize=3");
            var draftTask = Api.GetAsync<PagedResult<CampaignRow>>("api/campaigns?filter=status:eq:Draft&sort=startDate&pageSize=1");
            var ganttTasks = projects.Where(p => p.Status == "Active")
                .Select(p => (Project: p, Task: Api.GetAsync<ProjectGantt>($"api/projects/{p.Id}/gantt"))).ToList();
            await Task.WhenAll(new Task[] { overdueTask, outTask, draftTask }.Concat(ganttTasks.Select(g => (Task)g.Task)));

            var overdue = overdueTask.Result;
            if (overdue.TotalCount > 0)
            {
                var meta = string.Join(" · ", overdue.Items.Select((o, i) =>
                    $"{(i == 0 ? "Newest: " : "")}{o.CustomerName} #{o.Id}, due {Format.ShortDate(o.RequiredDate)}"));
                items.Add(new($"{Format.Number(overdue.TotalCount)} orders are overdue", meta, "orders", "alert-triangle", AttentionTone.Error));
            }

            var low = overview?.LowStockProducts.Where(p => p.UnitsInStock > 0).ToList() ?? [];
            if (outTask.Result.Items.FirstOrDefault() is { } outOfStock)
            {
                var meta = $"0 in stock · {outOfStock.UnitsOnOrder} on order · reorder level {outOfStock.ReorderLevel}.";
                if (low.FirstOrDefault() is { } l) meta += $" {l.Name} also below reorder ({l.UnitsInStock} / {l.ReorderLevel}).";
                items.Add(new($"{outOfStock.Name} is out of stock", meta, "products", "package", AttentionTone.Warning));
            }
            else if (low.FirstOrDefault() is { } l)
            {
                items.Add(new($"{l.Name} is below reorder level", $"{l.UnitsInStock} in stock · reorder level {l.ReorderLevel} · {l.UnitsOnOrder} on order", "products", "package", AttentionTone.Warning));
            }

            foreach (var (project, task) in ganttTasks)
            {
                var behind = task.Result.Tasks
                    .Where(t => !t.IsSummary && !t.IsMilestone && t.PercentComplete < 100)
                    .FirstOrDefault(t => t.Finish.Date < asOf || (t.Start.Date < asOf && t.PercentComplete == 0));
                if (behind is null) continue;
                var who = string.IsNullOrWhiteSpace(behind.Resources) ? "" : $" · {behind.Resources}";
                items.Add(new($"{behind.Name} is behind schedule",
                    $"{project.Name} · started {Format.ShortDate(behind.Start)}, {behind.PercentComplete:0}% complete{who}", "projects", "clock", AttentionTone.Warning));
                break;
            }

            if (draftTask.Result.Items.FirstOrDefault() is { } draft)
            {
                items.Add(new($"{draft.Channel} campaign starts {Format.ShortDate(draft.StartDate)}",
                    $"{draft.Name} is still a draft · {Format.Money0(draft.Budget)} budget", "campaigns", "info", AttentionTone.Info));
            }
        }
        catch (ApiException ex)
        {
            // The rest of the dashboard is still useful; log and show what we have.
            Logger.LogWarning("Needs-attention list incomplete: {Title}", ex.Problem.Title);
        }
        return items;
    }

    private static string AttentionIconClass(AttentionTone tone) => tone switch
    {
        AttentionTone.Error => "att-icon att-icon--error",
        AttentionTone.Warning => "att-icon att-icon--warning",
        _ => "att-icon att-icon--info"
    };

    // ---------- Projects & campaigns ----------

    private static string ProjectStatus(string status) => status == "OnHold" ? "On hold" : status;
    private static string ProjectTone(string status) => status == "OnHold" ? "warning" : "info";
    private static string ProjectBarClass(string status) => status == "OnHold" ? "proj-bar lp--amber" : "proj-bar";

    private static string ProjectMeta(ProjectRow p) =>
        $"{Format.ShortDate(p.StartDate)} – {Format.ShortDate(p.FinishDate)} · {p.ManagerName ?? "Unassigned"} · {Format.Compact(p.Budget)} budget";

    private static string CampaignMeta(CampaignRow c) =>
        $"{(c.Channel == "DirectMail" ? "Direct mail" : c.Channel)} · {c.LeadCount} leads · ends {Format.ShortDate(c.EndDate)}";

    private static double SpendPercent(CampaignRow c) => c.Budget == 0 ? 0 : (double)Math.Min(100m, c.ActualCost / c.Budget * 100m);

    private static string SpendText(CampaignRow c) => $"{Format.Compact(c.ActualCost)} spent";
    private static string BudgetText(CampaignRow c) => $"of {Format.Compact(c.Budget)}";

    private void GoNewOrder() => Nav.NavigateTo("orders/new");
}
