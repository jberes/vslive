using System.Globalization;
using ApexCharts;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Products;
using modern_windows_forms.Client.Services;
using Format = modern_windows_forms.Client.Shared.Format;

namespace modern_windows_forms.Client.Pages;

public partial class Products
{
    private const string Discontinued = "Discontinued";
    private const string OutOfStock = "Out of stock";
    private const string Reorder = "Reorder";
    private const string InStock = "In stock";

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private ILogger<Products> Logger { get; set; } = default!;

    private readonly int year = DateTime.Today.Year;
    private List<CategoryItem> categories = [];
    private int? supplierCount;
    private CategoryItem? selected;
    private CategoryView? view;
    private readonly Dictionary<int, CategoryView> cache = [];
    private ProblemDetails? pageError;
    private bool lowOnly;
    private int loadVersion;

    private int TotalProducts => categories.Sum(c => c.ProductCount);
    private static int CurrentQuarter => (DateTime.Today.Month - 1) / 3 + 1;

    private IReadOnlyList<StockRow> GridRows =>
        view is null ? [] : lowOnly ? view.Rows.Where(r => r.Status is Reorder or OutOfStock).ToList() : view.Rows;

    // Rows are 48px; show them all (categories hold at most ~13 products).
    private string GridHeight => $"{40 + GridRows.Count * 48 + 2}px";
    private string GridKey => $"{selected?.Id}-{lowOnly}";

    private string ReorderSummary => view is null || view.NeedsReorder.Count == 0
        ? "All above reorder level"
        : string.Join(", ", view.NeedsReorder.Take(2)) + (view.NeedsReorder.Count > 2 ? "…" : string.Empty);

    // ---------- Chart options (ApexCharts design system: chart-1…5, 85% bars, horizontal gridlines only) ----------

    private readonly ApexChartOptions<QuarterPoint> quarterOptions = new()
    {
        Colors = ["#a6d8fd", "#008ffb"],
        Fill = new Fill { Opacity = 0.9 },
        PlotOptions = new PlotOptions { Bar = new PlotOptionsBar { ColumnWidth = "58%", BorderRadius = 3, DataLabels = new PlotOptionsBarDataLabels { Position = BarDataLabelPosition.Top } } },
        DataLabels = new DataLabels
        {
            Enabled = true,
            EnabledOnSeries = [1],
            OffsetY = -18,
            Formatter = "function (v) { return '$' + (v / 1000).toFixed(1) + 'k'; }",
            Style = new DataLabelsStyle { FontSize = "11px", FontWeight = "600", Colors = ["#1a1a2e"] }
        },
        Legend = new Legend { Position = LegendPosition.Top, HorizontalAlign = Align.Right, FontSize = "12px" },
        Grid = new Grid { BorderColor = "#f0f0f0", Xaxis = new GridXAxis { Lines = new Lines { Show = false } } }
    };

    private readonly ApexChartOptions<ProductSales> productOptions = new()
    {
        Colors = ["#00b0a0"],
        Fill = new Fill { Opacity = 0.85 },
        PlotOptions = new PlotOptions { Bar = new PlotOptionsBar { Horizontal = true, BarHeight = "62%", BorderRadius = 3 } },
        Grid = new Grid
        {
            BorderColor = "#f0f0f0",
            Xaxis = new GridXAxis { Lines = new Lines { Show = true } },
            Yaxis = new GridYAxis { Lines = new Lines { Show = false } }
        }
    };

    protected override async Task OnInitializedAsync() => await LoadCategoriesAsync();

    private async Task RetryAsync()
    {
        pageError = null;
        if (categories.Count == 0) await LoadCategoriesAsync();
        else if (selected is not null) await LoadCategoryAsync(selected);
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var catsTask = Api.GetAsync<PagedResult<CategoryItem>>("api/categories?pageSize=100&sort=name");
            var suppliersTask = Api.GetAsync<PagedResult<object>>("api/suppliers?pageSize=1");
            categories = [.. (await catsTask).Items];
            try { supplierCount = (await suppliersTask).TotalCount; }
            catch (ApiException ex) { Logger.LogWarning("Supplier count unavailable: {Title}", ex.Problem.Title); }

            if (categories.Count > 0) await SelectCategoryAsync(categories[0]);
        }
        catch (ApiException ex)
        {
            pageError = ex.Problem;
        }
    }

    private async Task SelectCategoryAsync(CategoryItem cat)
    {
        if (selected?.Id == cat.Id && view is not null) return;
        selected = cat;
        lowOnly = false;
        if (cache.TryGetValue(cat.Id, out var cached))
        {
            view = cached;
            return;
        }
        await LoadCategoryAsync(cat);
    }

    private async Task LoadCategoryAsync(CategoryItem cat)
    {
        var version = ++loadVersion;
        view = null;
        pageError = null;
        try
        {
            var lastYearEnd = DateTime.Today.AddYears(-1).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var thisYearTask = Api.GetAsync<CategoryDashboard>($"api/categories/{cat.Id}/dashboard?year={year}");
            var lastYearTask = Api.GetAsync<CategoryDashboard>($"api/categories/{cat.Id}/dashboard?year={year - 1}");
            var productsTask = Api.GetAsync<PagedResult<ProductItem>>(
                "api/products?pageSize=200&filter=" + Uri.EscapeDataString($"categoryName:eq:{cat.Name}"));
            var samePeriodTask = SumLastYearSamePeriodAsync(cat.Name, $"{year - 1}-01-01", lastYearEnd);

            var thisYear = await thisYearTask;
            var lastYear = await lastYearTask;
            var products = await productsTask;
            var samePeriod = await samePeriodTask;
            if (version != loadVersion) return;

            var built = CategoryView.Build(thisYear, lastYear, products.Items, samePeriod);
            cache[cat.Id] = built;
            view = built;
        }
        catch (ApiException ex)
        {
            if (version == loadVersion) pageError = ex.Problem;
        }
    }

    /// <summary>Revenue for the same calendar window last year (Jan 1 → today's date), summed from order lines.</summary>
    private async Task<decimal> SumLastYearSamePeriodAsync(string category, string from, string toExclusive)
    {
        decimal total = 0;
        var page = 1;
        while (true)
        {
            var url = "api/order-details?pageSize=1000&page=" + page
                      + "&filter=" + Uri.EscapeDataString($"categoryName:eq:{category}")
                      + "&filter=" + Uri.EscapeDataString($"orderDate:ge:{from}")
                      + "&filter=" + Uri.EscapeDataString($"orderDate:lt:{toExclusive}");
            var result = await Api.GetAsync<PagedResult<OrderLine>>(url);
            total += result.Items.Sum(l => l.LineTotal);
            if (page >= result.TotalPages) return total;
            page++;
        }
    }

    // ---------- Grid helpers ----------

    private StockRow? RowFor(IgbCellTemplateContext ctx)
    {
        var key = ctx.Cell?.Id?.RowID;
        if (key is null || view is null) return null;
        return int.TryParse(Convert.ToString(key, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var id)
            ? view.Rows.FirstOrDefault(r => r.Id == id)
            : null;
    }

    private static string StockTrackClass(StockRow r) => r.Status switch
    {
        Discontinued => "stock-track stock-track--off",
        OutOfStock => "stock-track stock-track--out",
        Reorder => "stock-track stock-track--low",
        _ => "stock-track"
    };

    /// <summary>Bar width and reorder tick position as CSS custom properties (data-driven).</summary>
    private static string StockStyle(StockRow r)
    {
        var scale = Math.Max(Math.Max(r.UnitsInStock, r.ReorderLevel * 2), 40);
        var fill = Math.Min(100.0, r.UnitsInStock * 100.0 / scale);
        var tick = r.ReorderLevel * 100.0 / scale;
        return string.Create(CultureInfo.InvariantCulture, $"--fill:{fill:0.#}%;--tick:{tick:0.#}%");
    }

    private static string StatusTone(string status) => status switch
    {
        Discontinued => "neutral",
        OutOfStock => "error",
        Reorder => "warning",
        _ => "success"
    };

    private string PillClass(CategoryItem cat) => cat.Id == selected?.Id ? "cat-pill cat-pill--on" : "cat-pill";

    private Task NewProduct() =>
        Notifications.ToastAsync("The new-product form isn't part of this prototype yet.");

    /// <summary>Everything the category view renders, computed once per category.</summary>
    private sealed record CategoryView(
        decimal Revenue, decimal LastYearSamePeriod, decimal RevenueDelta, int Units, int Active,
        IReadOnlyList<ProductItem> Products, IReadOnlyList<string> NeedsReorder,
        IReadOnlyList<QuarterPoint> ThisYearQuarters, IReadOnlyList<QuarterPoint> LastYearQuarters,
        IReadOnlyList<ProductSales> TopProducts, IReadOnlyList<StockRow> Rows)
    {
        public static CategoryView Build(CategoryDashboard thisYear, CategoryDashboard lastYear, IReadOnlyList<ProductItem> products, decimal samePeriod)
        {
            var salesByName = thisYear.SalesByProduct.ToDictionary(p => p.Product, p => p.Sales);
            var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;

            static string StatusOf(ProductItem p) =>
                p.Discontinued ? Discontinued
                : p.UnitsInStock == 0 ? OutOfStock
                : p.ReorderLevel > 0 && p.UnitsInStock <= p.ReorderLevel ? Reorder
                : InStock;

            var rows = products
                .Select(p => new StockRow
                {
                    Id = p.Id,
                    Name = p.Name,
                    SupplierName = p.SupplierName,
                    QuantityPerUnit = p.QuantityPerUnit ?? string.Empty,
                    UnitPrice = p.UnitPrice,
                    UnitsInStock = p.UnitsInStock,
                    ReorderLevel = p.ReorderLevel,
                    UnitsOnOrder = p.UnitsOnOrder,
                    Status = StatusOf(p),
                    SalesYtd = salesByName.GetValueOrDefault(p.Name)
                })
                .OrderBy(r => r.Status == Discontinued)
                .ThenByDescending(r => r.SalesYtd)
                .ToList();

            List<QuarterPoint> Quarters(CategoryDashboard d, bool isThisYear) =>
                [.. Enumerable.Range(1, 4).Select(q => new QuarterPoint(
                    isThisYear && q == currentQuarter ? $"Q{q} (to date)" : $"Q{q}",
                    d.SalesByQuarter.FirstOrDefault(x => x.Name == $"Q{q}")?.Value ?? 0))];

            var thisQuarters = Quarters(thisYear, true);
            // Same category labels on both series so the bars group by quarter.
            var lastQuarters = Quarters(lastYear, false).Select((p, i) => p with { Quarter = thisQuarters[i].Quarter }).ToList();

            var delta = samePeriod > 0 ? (thisYear.TotalSales - samePeriod) / samePeriod * 100 : 0;
            return new CategoryView(
                Revenue: thisYear.TotalSales,
                LastYearSamePeriod: samePeriod,
                RevenueDelta: Math.Round(delta, 1),
                Units: thisYear.SalesByProduct.Sum(p => p.Units),
                Active: products.Count(p => !p.Discontinued),
                Products: products,
                NeedsReorder: rows.Where(r => r.Status is Reorder or OutOfStock).Select(r => r.Name).ToList(),
                ThisYearQuarters: thisQuarters,
                LastYearQuarters: lastQuarters,
                TopProducts: thisYear.SalesByProduct.Where(p => p.Sales > 0).OrderByDescending(p => p.Sales).Take(8).ToList(),
                Rows: rows);
        }
    }
}
