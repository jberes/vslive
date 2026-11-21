using System.Globalization;
using ApexCharts;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Customers;
using modern_windows_forms.Client.Services;
using modern_windows_forms.Client.Shared;
using Format = modern_windows_forms.Client.Shared.Format;

namespace modern_windows_forms.Client.Components.Customers;

/// <summary>Customer detail panel: header facts plus Overview / Orders / Products tabs.</summary>
public partial class CustomerDetail
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;

    [Parameter] public int? CustomerId { get; set; }

    private int? loadedId;
    private CustomerDashboard? dashboard;
    private List<YearRevenue>? yearRevenue;
    private ProblemDetails? error;
    private CancellationTokenSource? cts;

    private readonly ApexChartOptions<YearRevenue> yearOptions = new()
    {
        Colors = ["#008ffb"],
        Fill = new Fill { Opacity = 0.85 },
        PlotOptions = new PlotOptions { Bar = new PlotOptionsBar { ColumnWidth = "55%", BorderRadius = 4 } },
        Grid = new Grid { BorderColor = "#e5e7eb", Xaxis = new GridXAxis { Lines = new Lines { Show = false } } },
        Tooltip = new Tooltip { Enabled = true }
    };

    private readonly ApexChartOptions<NameValue> donutOptions = new()
    {
        Colors = ["#008ffb", "#00e396", "#feb019", "#ff4560", "#775dd0", "#00b0a0", "#3aa9ff", "#9ca3af"],
        Legend = new Legend { Position = LegendPosition.Right, FontSize = "12px" },
        Stroke = new Stroke { Width = 2, Colors = ["#ffffff"] },
        PlotOptions = new PlotOptions { Pie = new PlotOptionsPie { Donut = new PlotOptionsDonut { Size = "62%" } } }
    };

    private IEnumerable<CustomerTopProduct> TopProducts => dashboard?.TopProducts.Take(5) ?? [];
    private double TopProductMax => (double)(dashboard?.TopProducts.FirstOrDefault()?.Sales ?? 1m);

    protected override async Task OnParametersSetAsync()
    {
        if (CustomerId == loadedId) return;
        loadedId = CustomerId;
        await LoadAsync();
    }

    private Task ReloadAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        var token = cts.Token;

        dashboard = null;
        yearRevenue = null;
        error = null;
        if (CustomerId is not int id) return;

        try
        {
            var dashboardTask = Api.GetAsync<CustomerDashboard>($"api/customers/{id}/dashboard", token);
            var ordersTask = Api.GetAsync<PagedResult<CustomerOrder>>($"api/customers/{id}/orders?pageSize=1000", token);
            dashboard = await dashboardTask;
            var orders = await ordersTask;
            if (token.IsCancellationRequested) return;
            yearRevenue = BuildYearRevenue(orders.Items);
        }
        catch (ApiException ex)
        {
            if (!token.IsCancellationRequested) error = ex.Problem;
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer selection
        }
    }

    /// <summary>Order subtotals per calendar year, from the first order year through this year (gaps shown as zero).</summary>
    private static List<YearRevenue> BuildYearRevenue(IReadOnlyList<CustomerOrder> orders)
    {
        var thisYear = DateTime.Today.Year;
        var byYear = orders.GroupBy(o => o.OrderDate.Year).ToDictionary(g => g.Key, g => g.Sum(o => o.Subtotal));
        var first = byYear.Count == 0 ? thisYear : Math.Min(byYear.Keys.Min(), thisYear);
        first = Math.Max(first, thisYear - 5);
        return Enumerable.Range(first, thisYear - first + 1)
            .Select(y => new YearRevenue(y.ToString(CultureInfo.InvariantCulture), Math.Round(byYear.GetValueOrDefault(y), 2)))
            .ToList();
    }

    private static string OrderSub(CustomerOrder o) => o.ShippedDate is { } shipped
        ? $"Shipped {shipped.ToString("MMM d", Culture)}"
        : o.RequiredDate is { } due ? $"Due {due.ToString("MMM d", Culture)}" : o.Status;

    private static string AvatarCss(string name) => "detail-avatar " + Format.AvatarClass(name);

    private Task EditCustomer() =>
        Notifications.ToastAsync("Editing customers isn't part of this prototype yet.");
}
