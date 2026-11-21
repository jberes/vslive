using System.Collections;
using System.Globalization;
using System.Text.Json;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Orders;
using modern_windows_forms.Client.Services;
using modern_windows_forms.Client.Shared;

namespace modern_windows_forms.Client.Pages;

public partial class Orders
{
    private const int PageSize = 25;

    /// <summary>The ship POST is deferred this long so the snackbar's Undo can cancel it (the API has no "unship").</summary>
    private static readonly TimeSpan UndoWindow = TimeSpan.FromMilliseconds(6500);

    private static readonly (string Key, string Label, string? Status)[] Tabs =
    [
        ("all", "All", null),
        ("pending", "Pending", "Pending"),
        ("overdue", "Overdue", "Overdue"),
        ("shipped", "Shipped", "Shipped"),
    ];

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private ILogger<Orders> Logger { get; set; } = default!;

    /// <summary>Prefills the search box, e.g. from "View in Orders" after creating an order.</summary>
    [SupplyParameterFromQuery(Name = "search")] public string? SearchQuery { get; set; }

    [SupplyParameterFromQuery(Name = "status")] public string? StatusQuery { get; set; }

    private IgbGrid? grid;
    private IgbGridToolbarExporter? exporter;
    private IgbDialog? shipDialog;

    private List<OrderRow> rows = [];
    private readonly Dictionary<int, OrderRow> rowsById = [];
    private readonly Dictionary<int, IReadOnlyList<OrderLine>> linesByOrder = [];
    private readonly Dictionary<string, int> tabCounts = [];
    private Dictionary<int, string> employeeNames = [];

    private List<Shipper> shippers = [];
    private List<CountryCount> countries = [];
    private List<OrderFilter> filters = [];

    private string activeTab = "all";
    private string searchText = string.Empty;
    private string sortParam = "-orderDate";
    // IgbSortingExpression is a plain expression object here (initial grid sort), not a rendered component.
#pragma warning disable BL0005
    private IgbSortingExpression[] sortExpressions = [new() { FieldName = nameof(OrderRow.OrderDate), Dir = SortingDirection.Desc }];
#pragma warning restore BL0005
    private int page = 1;
    private int totalPages = 1;
    private int totalCount;
    private bool loading = true;
    private ProblemDetails? loadError;
    private CancellationTokenSource? searchDebounce;
    private CancellationTokenSource? loadCts;

    // Ship dialog state
    private OrderRow? shipTarget;
    private string shipViaValue = "1";
    private DateTime? shipDate = DateTime.Today;
    private string freightText = "0";
    private string? freightError;

    private readonly Dictionary<int, PendingShipment> pendingShipments = [];

    private sealed record PendingShipment(OrderRow Order, OrderRowSnapshot Before, ShipOrderInput Input, decimal? NewFreight, CancellationTokenSource Cts);

    private sealed record OrderRowSnapshot(DateTime? ShippedDate, string Status, int ShipViaId, string? ShipViaName, decimal Freight, decimal Total);

    protected override async Task OnInitializedAsync()
    {
        searchText = SearchQuery ?? string.Empty;
        if (Tabs.Any(t => t.Key == StatusQuery?.ToLowerInvariant())) activeTab = StatusQuery!.ToLowerInvariant();

        if (string.IsNullOrEmpty(SearchQuery))
        {
            // The design's default view: recent orders over $400.
            filters =
            [
                new("last30", "Ordered: last 30 days", $"orderDate:ge:{DateTime.Today.AddDays(-30):yyyy-MM-dd}"),
                new("over400", "Total: over $400", "total:gt:400"),
            ];
        }

        await LoadReferenceDataAsync();
        await Task.WhenAll(LoadCountsAsync(), LoadPageAsync());
    }

    private async Task LoadReferenceDataAsync()
    {
        try
        {
            var shipperTask = Api.GetAsync<PagedResult<Shipper>>("api/shippers?sort=companyName");
            var employeeTask = Api.GetAsync<List<EmployeeLookup>>("api/employees/lookup");
            var countryTask = Api.GetAsync<List<CountryCount>>("api/customers/countries");
            await Task.WhenAll(shipperTask, employeeTask, countryTask);
            shippers = [.. shipperTask.Result.Items];
            employeeNames = employeeTask.Result.ToDictionary(e => e.Id, e => e.Name);
            countries = [.. countryTask.Result.Take(15)];
        }
        catch (ApiException ex)
        {
            loadError = ex.Problem;
        }
    }

    private async Task LoadCountsAsync()
    {
        try
        {
            var tasks = Tabs.ToDictionary(t => t.Key, t => Api.GetAsync<PagedResult<OrderRow>>(
                "api/orders?pageSize=1" + (t.Status is null ? "" : "&filter=status:eq:" + t.Status)));
            await Task.WhenAll(tasks.Values);
            foreach (var (key, task) in tasks) tabCounts[key] = task.Result.TotalCount;
        }
        catch (ApiException ex)
        {
            Logger.LogWarning("Order counts unavailable: {Title}", ex.Problem.Title);
        }
        StateHasChanged();
    }

    private async Task LoadPageAsync()
    {
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        var ct = loadCts.Token;
        loading = true;
        StateHasChanged();
        try
        {
            var result = await Api.GetAsync<PagedResult<OrderRow>>(BuildQuery(), ct);
            if (ct.IsCancellationRequested) return;
            foreach (var row in result.Items)
            {
                row.EmployeeFullName = employeeNames.TryGetValue(row.EmployeeId, out var name) ? name : row.EmployeeName;
                if (pendingShipments.TryGetValue(row.Id, out var pending)) ApplyShipped(row, pending.Input, pending.NewFreight);
            }
            rows = [.. result.Items];
            rowsById.Clear();
            foreach (var row in rows) rowsById[row.Id] = row;
            totalCount = result.TotalCount;
            totalPages = Math.Max(1, result.TotalPages);
            loadError = null;
        }
        catch (OperationCanceledException) { return; }
        catch (ApiException ex)
        {
            loadError = ex.Problem;
            rows = [];
        }
        finally
        {
            if (!ct.IsCancellationRequested) loading = false;
        }
        StateHasChanged();
    }

    private string BuildQuery()
    {
        var q = new List<string> { $"page={page}", $"pageSize={PageSize}", "sort=" + Uri.EscapeDataString(sortParam) };
        var status = Tabs.First(t => t.Key == activeTab).Status;
        if (status is not null) q.Add("filter=status:eq:" + status);
        q.AddRange(filters.Select(f => "filter=" + Uri.EscapeDataString(f.Query)));
        if (!string.IsNullOrWhiteSpace(searchText)) q.Add("search=" + Uri.EscapeDataString(searchText.Trim()));
        return "api/orders?" + string.Join("&", q);
    }

    private Task ReloadAsync() => Task.WhenAll(LoadCountsAsync(), LoadPageAsync());

    // ---------- Tabs, search, filters, paging, sorting ----------

    private async Task OnTabSelected(string key, bool selected)
    {
        if (!selected || key == activeTab) return;
        activeTab = key;
        page = 1;
        await LoadPageAsync();
    }

    private string CountText(string key) => tabCounts.TryGetValue(key, out var n) ? Format.Number(n) : "…";

    private string TabCountClass(string key) =>
        key == "overdue" ? "tab-count tab-count--error" : key == activeTab ? "tab-count tab-count--on" : "tab-count";

    private async Task OnSearchChanged()
    {
        searchDebounce?.Cancel();
        searchDebounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(350, searchDebounce.Token);
        }
        catch (TaskCanceledException) { return; }
        page = 1;
        await LoadPageAsync();
    }

    private Task RemoveFilterAsync(OrderFilter filter)
    {
        filters = filters.Where(f => f.Key != filter.Key).ToList();
        page = 1;
        return LoadPageAsync();
    }

    private Task ClearFiltersAsync()
    {
        filters = [];
        page = 1;
        return LoadPageAsync();
    }

    private Task ClearAllAsync()
    {
        filters = [];
        searchText = string.Empty;
        page = 1;
        return LoadPageAsync();
    }

    private Task OnShipViaPicked(IgbDropdownItemComponentEventArgs e)
    {
        var shipper = shippers.FirstOrDefault(s => s.Id.ToString() == e.Detail?.Value);
        return shipper is null ? Task.CompletedTask
            : AddFilterAsync(new OrderFilter("shipvia", "Ship via: " + shipper.CompanyName, $"shipViaId:eq:{shipper.Id}"));
    }

    private Task OnCountryPicked(IgbDropdownItemComponentEventArgs e) =>
        string.IsNullOrEmpty(e.Detail?.Value) ? Task.CompletedTask
            : AddFilterAsync(new OrderFilter("country", "Country: " + e.Detail.Value, "shipCountry:eq:" + e.Detail.Value));

    /// <summary>One chip per filter kind: picking a new shipper/country replaces the previous chip.</summary>
    private Task AddFilterAsync(OrderFilter filter)
    {
        filters = filters.Where(f => f.Key != filter.Key).Append(filter).ToList();
        page = 1;
        return LoadPageAsync();
    }

    private Task PreviousPageAsync()
    {
        if (page <= 1) return Task.CompletedTask;
        page--;
        return LoadPageAsync();
    }

    private Task NextPageAsync()
    {
        if (page >= totalPages) return Task.CompletedTask;
        page++;
        return LoadPageAsync();
    }

    /// <summary>Server-side sort: the grid's sort expression is translated to the API's <c>sort=-field</c> syntax.</summary>
    private Task OnSortingDone(IgbSortingExpressionEventArgs e)
    {
        var expr = e.Detail?.FirstOrDefault(x => x.Dir != SortingDirection.None);
        if (expr is null)
        {
            sortParam = "-orderDate";
            sortExpressions = [];
        }
        else
        {
            var field = expr.FieldName switch
            {
                nameof(OrderRow.EmployeeFullName) => "employeeName",
                _ => JsonNamingPolicy.CamelCase.ConvertName(expr.FieldName)
            };
            sortParam = (expr.Dir == SortingDirection.Desc ? "-" : "") + field;
            sortExpressions = [expr];
        }
        page = 1;
        return LoadPageAsync();
    }

    private string FooterText
    {
        get
        {
            if (loading && rows.Count == 0) return "Loading orders…";
            var scope = activeTab == "all" ? "orders" : $"{activeTab} orders";
            var first = totalCount == 0 ? 0 : (page - 1) * PageSize + 1;
            var last = Math.Min(page * PageSize, totalCount);
            var order = sortParam == "-orderDate" ? " · newest first" : "";
            return $"Showing {first:N0}–{last:N0} of {totalCount:N0} {scope}{order}";
        }
    }

    // ---------- Grid templates ----------

    /// <summary>Resolves the row behind a templated cell through its primary key (the grid hands templates a serialized copy).</summary>
    private OrderRow? RowFor(IgbCellTemplateContext ctx) =>
        int.TryParse(ctx.Cell?.Id?.RowID, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && rowsById.TryGetValue(id, out var row)
            ? row : null;

    private OrderRow? RowForDetail(IgbGridMasterDetailContext ctx)
    {
        var id = ctx.Implicit switch
        {
            OrderRow r => r.Id,
            IDictionary<string, object> d when d.TryGetValue(nameof(OrderRow.Id), out var v) => Convert.ToInt32(v, CultureInfo.InvariantCulture),
            IDictionary d when d.Contains(nameof(OrderRow.Id)) => Convert.ToInt32(d[nameof(OrderRow.Id)], CultureInfo.InvariantCulture),
            JsonElement { ValueKind: JsonValueKind.Object } j when j.TryGetProperty(nameof(OrderRow.Id), out var p) => p.GetInt32(),
            _ => -1
        };
        if (id < 0 && ctx.Index >= 0 && ctx.Index < rows.Count) id = rows[(int)ctx.Index].Id;
        return rowsById.TryGetValue(id, out var row) ? row : null;
    }

    private IReadOnlyList<OrderLine>? LinesFor(int orderId) => linesByOrder.GetValueOrDefault(orderId);

    /// <summary>Caches lines an expanded row's detail panel loaded, so re-expanding doesn't refetch.</summary>
    private void CacheLines(int orderId, IReadOnlyList<OrderLine> lines) => linesByOrder[orderId] = [.. lines];

    // ---------- Export ----------

    /// <summary>Exports the rows currently in the grid (the loaded page) through the grid's built-in CSV exporter.</summary>
    private async Task ExportCsvAsync()
    {
        if (exporter is null) return;
        await exporter.ExportGridAsync(GridToolbarExporterType.Csv);
        await Notifications.ToastAsync($"Exported {rows.Count} orders on this page to orders.csv", ToastKind.Success);
    }

    // ---------- Ship order ----------

    private async Task OpenShipDialogAsync(OrderRow order)
    {
        shipTarget = order;
        shipViaValue = order.ShipViaId.ToString(CultureInfo.InvariantCulture);
        shipDate = DateTime.Today;
        freightText = order.Freight.ToString("0.00", CultureInfo.InvariantCulture);
        freightError = null;
        StateHasChanged();
        if (shipDialog is not null) await shipDialog.ShowAsync();
    }

    private async Task CloseShipDialogAsync()
    {
        if (shipDialog is not null) await shipDialog.HideAsync();
    }

    private async Task ConfirmShipAsync()
    {
        if (shipTarget is null) return;
        if (!decimal.TryParse(freightText, NumberStyles.Number, CultureInfo.InvariantCulture, out var freight) || freight < 0)
        {
            freightError = "Enter a freight amount of 0 or more.";
            return;
        }

        var order = shipTarget;
        var input = new ShipOrderInput(BusinessDate.From(shipDate ?? DateTime.Today), int.TryParse(shipViaValue, out var via) ? via : order.ShipViaId);
        var newFreight = freight != order.Freight ? freight : (decimal?)null;
        var before = new OrderRowSnapshot(order.ShippedDate, order.Status, order.ShipViaId, order.ShipViaName, order.Freight, order.Total);

        await CloseShipDialogAsync();

        // Optimistic update; the API call is deferred so Undo can cancel it.
        ApplyShipped(order, input, newFreight);
        var pending = new PendingShipment(order, before, input, newFreight, new CancellationTokenSource());
        pendingShipments[order.Id] = pending;
        RefreshGrid();

        await Notifications.SnackbarAsync($"Order #{order.Id} marked as shipped", "Undo", () => UndoShipAsync(order.Id));
        _ = CommitAfterDelayAsync(pending);
    }

    private void ApplyShipped(OrderRow order, ShipOrderInput input, decimal? newFreight)
    {
        order.ShippedDate = input.ShippedDate;
        order.Status = "Shipped";
        if (input.ShipViaId is int via)
        {
            order.ShipViaId = via;
            order.ShipViaName = shippers.FirstOrDefault(s => s.Id == via)?.CompanyName ?? order.ShipViaName;
        }
        if (newFreight is decimal f)
        {
            order.Total += f - order.Freight;
            order.Freight = f;
        }
    }

    private static void Restore(OrderRow order, OrderRowSnapshot s)
    {
        order.ShippedDate = s.ShippedDate;
        order.Status = s.Status;
        order.ShipViaId = s.ShipViaId;
        order.ShipViaName = s.ShipViaName;
        order.Freight = s.Freight;
        order.Total = s.Total;
    }

    private async Task UndoShipAsync(int orderId)
    {
        if (!pendingShipments.Remove(orderId, out var pending)) return;
        pending.Cts.Cancel();
        Restore(pending.Order, pending.Before);
        RefreshGrid();
        await Notifications.ToastAsync($"Order #{orderId} is pending again");
    }

    private async Task CommitAfterDelayAsync(PendingShipment pending)
    {
        try
        {
            await Task.Delay(UndoWindow, pending.Cts.Token);
        }
        catch (TaskCanceledException) { return; }
        await CommitAsync(pending);
    }

    private async Task CommitAsync(PendingShipment pending)
    {
        if (!pendingShipments.Remove(pending.Order.Id)) return;
        var order = pending.Order;
        try
        {
            if (pending.NewFreight is decimal freight)
            {
                // Freight is a header field: PUT the header back unchanged except for freight.
                await Api.PutAsync<OrderRow>($"api/orders/{order.Id}", new OrderInput
                {
                    CustomerId = order.CustomerId,
                    EmployeeId = order.EmployeeId,
                    ShipViaId = pending.Input.ShipViaId ?? order.ShipViaId,
                    OrderDate = order.OrderDate,
                    RequiredDate = order.RequiredDate,
                    Freight = freight,
                    ShipName = order.ShipName,
                    ShipAddress = order.ShipAddress,
                    ShipCity = order.ShipCity,
                    ShipRegion = order.ShipRegion,
                    ShipPostalCode = order.ShipPostalCode,
                    ShipCountry = order.ShipCountry,
                });
            }
            var shipped = await Api.PostAsync<OrderRow>($"api/orders/{order.Id}/ship", pending.Input);
            if (shipped is not null)
            {
                order.ShippedDate = shipped.ShippedDate;
                order.Status = shipped.Status;
                order.Total = shipped.Total;
            }
            if (!disposed) await LoadCountsAsync();
        }
        catch (ApiException ex)
        {
            Restore(order, pending.Before);
            await Notifications.ToastAsync(ex.Status == 409
                ? $"Order #{order.Id} was already shipped by someone else. Refresh to see the latest."
                : $"Couldn't ship order #{order.Id}: {ex.Problem.Detail ?? ex.Problem.Title}", ToastKind.Error);
        }
        if (!disposed) await InvokeAsync(RefreshGrid);
    }

    /// <summary>Row objects are mutated in place; hand the grid a new list instance so it re-binds.</summary>
    private void RefreshGrid()
    {
        rows = [.. rows];
        if (!disposed) StateHasChanged();
    }

    private bool disposed;

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        searchDebounce?.Cancel();
        loadCts?.Cancel();
        // Leaving the page commits any shipment still inside its undo window instead of dropping it.
        foreach (var pending in pendingShipments.Values.ToList())
        {
            pending.Cts.Cancel();
            await CommitAsync(pending);
        }
    }
}
