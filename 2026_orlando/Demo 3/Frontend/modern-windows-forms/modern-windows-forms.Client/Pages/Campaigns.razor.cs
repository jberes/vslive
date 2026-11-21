using System.Globalization;
using System.Text.Json;
using ApexCharts;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Marketing;
using modern_windows_forms.Client.Services;
using Format = modern_windows_forms.Client.Shared.Format;

namespace modern_windows_forms.Client.Pages;

public partial class Campaigns
{
    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;

    public sealed record FunnelStage(string Label, int Count, string Conversion);
    public sealed record ChannelSpend(string Name, decimal Spend, string Color);

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Funnel stage colors from the design (chart-1, teal accent, chart-5, chart-3, success).</summary>
    private static readonly string[] FunnelColors = ["#008ffb", "#00b0a0", "#775dd0", "#feb019", "#00a86f"];

    /// <summary>Channel colors from the design's donut (chart-1…5, teal accent, then gray).</summary>
    private static readonly Dictionary<string, string> ChannelColors = new()
    {
        ["Events"] = "#008ffb", ["Search"] = "#00e396", ["Social"] = "#feb019", ["Email"] = "#ff4560",
        ["Webinar"] = "#775dd0", ["Partner"] = "#00b0a0", ["DirectMail"] = "#9ca3af", ["Display"] = "#3aa9ff"
    };

    private static readonly (string Key, string Label)[] Filters = [("All", "All"), ("Active", "Active"), ("Completed", "Completed"), ("Draft", "Draft")];

    private IgbGrid? grid;
    private List<CampaignDto>? campaigns;
    private List<CampaignRow> rows = [];
    private Dictionary<int, CampaignRow> rowIndex = [];
    private string statusFilter = "All";
    private ProblemDetails? loadError;

    private int selectedId;
    private object[] selectedKeys = [];
    private CampaignPerformance? selected;
    private bool performanceLoading;
    private ProblemDetails? performanceError;
    private List<FunnelStage> funnel = [];
    private List<ChannelSpend> channels = [];

    private ApexChartOptions<FunnelStage> funnelOptions = new();
    private ApexChartOptions<ChannelSpend> donutOptions = new();

    private decimal TotalSpend => campaigns?.Sum(c => c.ActualCost) ?? 0;
    private decimal TotalBudget => campaigns?.Sum(c => c.Budget) ?? 0;
    private int BudgetUsed => TotalBudget == 0 ? 0 : (int)Math.Round(TotalSpend * 100 / TotalBudget);
    private int TotalLeads => campaigns?.Sum(c => c.LeadCount) ?? 0;
    private long TotalClicks => campaigns?.Sum(c => c.Clicks) ?? 0;
    private int TotalWon => campaigns?.Sum(c => c.WonCount) ?? 0;
    private decimal TotalWonValue => campaigns?.Sum(c => c.WonValue) ?? 0;

    private int CountByStatus(string status) => campaigns?.Count(c => c.Status == status) ?? 0;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        loadError = null;
        campaigns = null;
        try
        {
            var page = await Api.GetAsync<PagedResult<CampaignDto>>("api/campaigns?pageSize=200&sort=-startDate");
            // "Running in the last 12 months": ended within the last year, plus drafts/scheduled ones still ahead.
            var since = DateTime.Today.AddMonths(-12);
            campaigns = page.Items.Where(c => c.EndDate >= since).OrderByDescending(c => c.StartDate).ToList();
            BuildRows();
            BuildChannels();

            var initial = campaigns.FirstOrDefault(c => c.Status == "Completed") ?? campaigns.FirstOrDefault();
            if (initial is not null) await SelectAsync(initial.Id);
        }
        catch (ApiException ex)
        {
            loadError = ex.Problem;
        }
    }

    private void BuildRows()
    {
        if (campaigns is null) return;
        rows = campaigns
            .Where(c => statusFilter == "All" || c.Status == statusFilter)
            .Select(c => new CampaignRow
            {
                Id = c.Id,
                Name = c.Name,
                Channel = ChannelLabel(c.Channel),
                Status = c.Status,
                Dates = DateRange(c),
                Budget = c.Budget,
                ActualCost = c.ActualCost,
                SpendPercent = c.Budget == 0 ? 0 : (double)Math.Round(c.ActualCost * 100 / c.Budget, 1),
                Leads = c.LeadCount,
                Ctr = c.Impressions == 0 ? "—" : ((double)c.Clicks * 100 / c.Impressions).ToString("0.00", Us) + "%",
                Owner = c.OwnerName ?? "—"
            })
            .ToList();
        rowIndex = rows.ToDictionary(r => r.Id);
        selectedKeys = rowIndex.ContainsKey(selectedId) ? [selectedId] : [];
    }

    private void BuildChannels()
    {
        if (campaigns is null) return;
        channels = campaigns
            .GroupBy(c => c.Channel)
            .Select(g => new ChannelSpend(ChannelLabel(g.Key), Math.Round(g.Sum(c => c.ActualCost), 2), ChannelColors.GetValueOrDefault(g.Key, "#9ca3af")))
            .Where(c => c.Spend > 0)
            .OrderByDescending(c => c.Spend)
            .ToList();

        var total = channels.Sum(c => c.Spend);
        donutOptions = new ApexChartOptions<ChannelSpend>
        {
            Chart = new Chart { Toolbar = new Toolbar { Show = false }, FontFamily = "Inter, sans-serif" },
            Colors = channels.Select(c => c.Color).ToList(),
            Legend = new Legend { Show = false },
            DataLabels = new DataLabels { Enabled = false },
            Stroke = new Stroke { Width = 2, Colors = ["#ffffff"] },
            Tooltip = new Tooltip { Y = new TooltipY { Formatter = "function(v){ return '$' + Math.round(v).toLocaleString('en-US'); }" } },
            PlotOptions = new PlotOptions
            {
                Pie = new PlotOptionsPie
                {
                    Donut = new PlotOptionsDonut
                    {
                        Size = "68%",
                        Labels = new DonutLabels
                        {
                            Show = true,
                            Name = new DonutLabelName { Show = true, FontSize = "11px", Color = "#6b7280", OffsetY = 18 },
                            Value = new DonutLabelValue { Show = true, FontSize = "20px", FontWeight = "600", Color = "#1a1a2e", OffsetY = -10,
                                Formatter = "function(v){ var n = Number(v); return n >= 1000 ? '$' + Math.round(n/1000) + 'k' : '$' + Math.round(n); }" },
                            Total = new DonutLabelTotal
                            {
                                Show = true, ShowAlways = true, Label = $"{channels.Count} channels", FontSize = "11px", Color = "#6b7280",
                                Formatter = $"function(){{ return '{Format.Compact(total)}'.replace(/\\.\\d+k/, 'k'); }}"
                            }
                        }
                    }
                }
            }
        };
    }

    private void SetFilter(string key)
    {
        statusFilter = key;
        BuildRows();
    }

    private CampaignRow? RowFor(object? rowId) => ToInt(rowId) is { } id ? rowIndex.GetValueOrDefault(id) : null;

    private async Task OnRowSelectionChanging(IgbRowSelectionEventArgs args)
    {
        var key = args.Detail.NewSelection?.FirstOrDefault();
        if (ToInt(key) is { } id && id != selectedId) await SelectAsync(id);
    }

    private async Task SelectAsync(int id)
    {
        selectedId = id;
        selectedKeys = [id];
        await LoadPerformanceAsync(id);
    }

    private async Task LoadPerformanceAsync(int id)
    {
        performanceLoading = true;
        performanceError = null;
        StateHasChanged();
        try
        {
            var perf = await Api.GetAsync<CampaignPerformance>($"api/campaigns/{id}/performance");
            if (id != selectedId) return; // a newer selection won
            selected = perf;
            BuildFunnel(perf);
        }
        catch (ApiException ex)
        {
            performanceError = ex.Problem;
        }
        finally
        {
            if (id == selectedId) performanceLoading = false;
        }
    }

    /// <summary>
    /// Lead funnel for the selected campaign, as in the design: all leads → reached Contacted → Qualified or later →
    /// Proposal or later → Won. The first conversion is leads ÷ clicks; the rest are "% of previous stage".
    /// </summary>
    private void BuildFunnel(CampaignPerformance perf)
    {
        int Get(string status) => (int)(perf.LeadsByStatus.FirstOrDefault(s => s.Name == status)?.Value ?? 0);
        var total = perf.LeadsByStatus.Sum(s => (int)s.Value);
        var stages = new (string Label, int Count)[]
        {
            ("Leads", total),
            ("Contacted", total - Get("New")),
            ("Qualified", Get("Qualified") + Get("Proposal") + Get("Won")),
            ("Proposal", Get("Proposal") + Get("Won")),
            ("Won", Get("Won"))
        };
        var clicks = perf.Campaign.Clicks;
        funnel = stages.Select((s, i) => new FunnelStage(s.Label, s.Count,
            i == 0
                ? (clicks > 0 ? ((double)total * 100 / clicks).ToString("0.00", Us) + "% of clicks" : "—")
                : (stages[i - 1].Count > 0 ? Math.Round(s.Count * 100.0 / stages[i - 1].Count) + "% of prev." : "—")))
            .ToList();

        funnelOptions = new ApexChartOptions<FunnelStage>
        {
            Chart = new Chart { Toolbar = new Toolbar { Show = false }, FontFamily = "Inter, sans-serif" },
            Colors = [.. FunnelColors],
            Legend = new Legend { Show = false },
            Grid = new Grid { Show = false, Padding = new Padding { Left = 0, Right = 0, Top = -10, Bottom = -10 } },
            Xaxis = new XAxis { Labels = new XAxisLabels { Show = false }, AxisBorder = new AxisBorder { Show = false }, AxisTicks = new AxisTicks { Show = false } },
            Yaxis = [new YAxis { Labels = new YAxisLabels { Show = true, Style = new AxisLabelStyle { FontSize = "13px", Colors = new ApexCharts.Color("#4b5563") } } }],
            Tooltip = new Tooltip { Enabled = true },
            Fill = new Fill { Opacity = 0.9 },
            DataLabels = new DataLabels
            {
                Enabled = true,
                Style = new DataLabelsStyle { FontSize = "13px", FontWeight = "600", Colors = ["#ffffff", "#ffffff", "#ffffff", "#1a1a2e", "#ffffff"] },
                DropShadow = new DropShadow { Enabled = false }
            },
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { Horizontal = true, IsFunnel = true, Distributed = true, BorderRadius = 6, BarHeight = "78%" }
            }
        };
    }

    private Task NewCampaignAsync() =>
        Notifications.ToastAsync("New campaign entry isn't part of this prototype yet.");

    public static string ChannelLabel(string channel) => channel == "DirectMail" ? "Direct mail" : channel;

    public static string DateRange(CampaignDto c) =>
        $"{c.StartDate.ToString("MMM d", Us)} – {c.EndDate.ToString("MMM d", Us)}";

    private static string StatusTone(string? status) => status switch
    {
        "Active" => "info",
        "Completed" => "success",
        "Paused" => "warning",
        _ => "neutral"
    };

    /// <summary>Grid row ids arrive from JS as numbers or JSON elements.</summary>
    private static int? ToInt(object? value) => value switch
    {
        null => null,
        CampaignRow row => row.Id,   // IgbGrid 26.1 reports selected row objects, not keys
        int i => i,
        long l => (int)l,
        double d => (int)d,
        decimal m => (int)m,
        JsonElement { ValueKind: JsonValueKind.Number } e => e.GetInt32(),
        JsonElement { ValueKind: JsonValueKind.String } e when int.TryParse(e.GetString(), out var p) => p,
        string s when int.TryParse(s, out var p) => p,
        _ => null
    };
}
