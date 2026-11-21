using System.Globalization;
using System.Text;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Customers;
using modern_windows_forms.Client.Services;
using modern_windows_forms.Client.Shared;

namespace modern_windows_forms.Client.Pages;

public partial class Customers : IDisposable
{
    private const string AllCountries = "All countries";

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private ILogger<Customers> Logger { get; set; } = default!;

    /// <summary>Global search in the app bar navigates to <c>/customers?search=…</c>.</summary>
    [SupplyParameterFromQuery(Name = "search")] public string? SearchQuery { get; set; }

    private IgbGrid? grid;
    private IgbPaginator? paginator;

    private List<CustomerRow> rows = [];
    private Dictionary<int, CustomerRow> rowsById = [];
    private List<CountryCount> countries = [];
    private int? allCustomers;
    private int totalRecords;
    private int pageIndex;
    private int perPage = 12;
    private readonly double[] pageSizeOptions = [12, 25, 50];
    private bool loading = true;
    private ProblemDetails? error;

    private string searchText = string.Empty;
    private string appliedSearch = string.Empty;
    private string? lastQuerySearch;
    private string countryValue = AllCountries;
    private bool openOnly;
    private IReadOnlyCollection<int>? openCustomerIds;

    private string sortField = "companyName";
    private bool sortDesc;
#pragma warning disable BL0005 // initial sort state handed to the grid, not mutating a rendered component
    private IgbSortingExpression[] sortExpressions = [new() { FieldName = "CompanyName", Dir = SortingDirection.Asc }];
#pragma warning restore BL0005

    private int? selectedId;
    private bool syncSelection;
    private CancellationTokenSource? searchDebounce;
    private int loadVersion;

    private bool CountryActive => countryValue != AllCountries;
    private bool HasFilters => CountryActive || openOnly || !string.IsNullOrWhiteSpace(appliedSearch);
    private bool IsEmpty => !loading && error is null && totalRecords == 0;

    // Header (38px) + 12 visible rows (56px) + paginator; more rows scroll inside the grid.
    private string GridHeight => $"{38 + Math.Min(perPage, 12) * 56 + 58}px";

    private string RangeText
    {
        get
        {
            if (totalRecords == 0) return string.Empty;
            var first = pageIndex * perPage + 1;
            var last = Math.Min(totalRecords, first + rows.Count - 1);
            var sortLabel = sortField switch
            {
                "companyName" => "company",
                "contactName" => "contact",
                "city" => "city",
                "orderCount" => "orders",
                _ => sortField
            };
            return $"Showing {first}–{last} of {Format.Number(totalRecords)} · sorted by {sortLabel}{(sortDesc ? " (descending)" : string.Empty)}";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            countries = (await Api.GetAsync<List<CountryCount>>("api/customers/countries"))
                .OrderBy(c => c.Country, StringComparer.CurrentCulture).ToList();
            allCustomers = countries.Sum(c => c.Customers);
        }
        catch (ApiException ex)
        {
            Logger.LogWarning("Countries unavailable: {Title}", ex.Problem.Title);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        // First load, or the global search changed the query string.
        if (lastQuerySearch is null || !string.Equals(lastQuerySearch, SearchQuery ?? string.Empty, StringComparison.Ordinal))
        {
            lastQuerySearch = SearchQuery ?? string.Empty;
            searchText = appliedSearch = lastQuerySearch.Trim();
            pageIndex = 0;
            await LoadPageAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (syncSelection && grid is not null && selectedId is int id && rowsById.ContainsKey(id))
        {
            syncSelection = false;
            // The grid's client element may not be ready right after its first render; retry briefly.
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    await grid.SelectRowsAsync([id], true);
                    break;
                }
                catch (InvalidOperationException)
                {
                    await Task.Delay(150);
                }
            }
        }
    }

    private async Task LoadPageAsync()
    {
        var version = ++loadVersion;
        loading = true;
        error = null;
        try
        {
            if (openOnly && openCustomerIds is null)
                openCustomerIds = await LoadOpenOrderCustomerIdsAsync();

            var result = await Api.GetAsync<PagedResult<CustomerRow>>(BuildQuery());
            if (version != loadVersion) return;   // a newer request superseded this one

            rows = [.. result.Items];
            rowsById = rows.ToDictionary(r => r.Id);
            totalRecords = result.TotalCount;

            // Keep the current selection when it is on this page; otherwise select the first row.
            if (selectedId is not int id || !rowsById.ContainsKey(id))
                selectedId = rows.FirstOrDefault()?.Id;
            syncSelection = selectedId is not null;
        }
        catch (ApiException ex)
        {
            if (version != loadVersion) return;
            error = ex.Problem;
            rows = [];
            rowsById = [];
            totalRecords = 0;
        }
        finally
        {
            if (version == loadVersion) loading = false;
        }
    }

    private string BuildQuery()
    {
        var q = new StringBuilder("api/customers?");
        q.Append("page=").Append(pageIndex + 1).Append("&pageSize=").Append(perPage);
        q.Append("&sort=").Append(sortDesc ? "-" : string.Empty).Append(sortField);
        if (sortField != "companyName") q.Append(",companyName");
        if (!string.IsNullOrWhiteSpace(appliedSearch)) q.Append("&search=").Append(Uri.EscapeDataString(appliedSearch));
        if (CountryActive) q.Append("&filter=").Append(Uri.EscapeDataString($"country:eq:{countryValue}"));
        if (openOnly)
        {
            // The API has no "open orders" field on customers: filter by the ids of customers with Pending/Overdue orders.
            var ids = openCustomerIds is { Count: > 0 } ? string.Join('|', openCustomerIds) : "0";
            q.Append("&filter=").Append(Uri.EscapeDataString($"id:in:{ids}"));
        }
        return q.ToString();
    }

    private async Task<IReadOnlyCollection<int>> LoadOpenOrderCustomerIdsAsync()
    {
        var open = await Api.GetAsync<PagedResult<CustomerOrder>>(
            "api/orders?pageSize=1000&filter=" + Uri.EscapeDataString("status:in:Pending|Overdue"));
        return open.Items.Select(o => o.CustomerId).Distinct().ToArray();
    }

    private CustomerRow? RowFor(IgbCellTemplateContext ctx)
    {
        var key = ctx.Cell?.Id?.RowID;
        if (key is null) return null;
        return int.TryParse(Convert.ToString(key, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var id)
               && rowsById.TryGetValue(id, out var row) ? row : null;
    }

    // ---------- Grid events ----------

    private void OnRowSelectionChanging(IgbRowSelectionEventArgs e)
    {
        // IgbGrid 26.1 reports the selected row objects (not primary keys) in NewSelection.
        var selected = e.Detail?.NewSelection?.FirstOrDefault();
        int? id = selected switch
        {
            null => null,
            CustomerRow row => row.Id,
            int i => i,
            _ => int.TryParse(Convert.ToString(selected, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null
        };
        if (id is null) return;   // keep the detail panel on the last customer when the selection is cleared
        selectedId = id;
    }

    private async Task OnSortingDone(IgbSortingExpressionEventArgs e)
    {
        var expr = e.Detail?.FirstOrDefault();
        if (expr is null || string.IsNullOrEmpty(expr.FieldName) || expr.Dir == SortingDirection.None)
        {
            sortField = "companyName";
            sortDesc = false;
        }
        else
        {
            sortField = char.ToLowerInvariant(expr.FieldName[0]) + expr.FieldName[1..];
            sortDesc = expr.Dir == SortingDirection.Desc;
        }
        pageIndex = 0;
        await LoadPageAsync();
    }

    private async Task OnPageChange(IgbNumberEventArgs e)
    {
        pageIndex = (int)e.Detail;
        await LoadPageAsync();
    }

    private async Task OnPerPageChange(IgbNumberEventArgs e)
    {
        perPage = (int)e.Detail;
        pageIndex = 0;
        await LoadPageAsync();
    }

    // ---------- Filters ----------

    private async Task OnSearchInput(IgbComponentValueChangedEventArgs e)
    {
        searchText = e.Detail ?? string.Empty;
        searchDebounce?.Cancel();
        var cts = searchDebounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        appliedSearch = searchText.Trim();
        await OnFiltersChanged();
    }

    private Task OnCountryChanged() => OnFiltersChanged();

    private async Task OnFiltersChanged()
    {
        if (!openOnly) openCustomerIds = null;   // refresh the open-order set next time it is switched on
        pageIndex = 0;
        await LoadPageAsync();
    }

    private Task ClearSearch()
    {
        searchText = appliedSearch = string.Empty;
        return OnFiltersChanged();
    }

    private Task ClearCountry()
    {
        countryValue = AllCountries;
        return OnFiltersChanged();
    }

    private Task ClearOpen()
    {
        openOnly = false;
        return OnFiltersChanged();
    }

    private Task ClearAll()
    {
        searchText = appliedSearch = string.Empty;
        countryValue = AllCountries;
        openOnly = false;
        return OnFiltersChanged();
    }

    // ---------- Actions ----------

    private Task NewCustomer() =>
        Notifications.ToastAsync("The new-customer form isn't part of this prototype yet.");

    /// <summary>CSV of the rows on the current page, served as a data URI for the Export button.</summary>
    private string ExportHref
    {
        get
        {
            var csv = new StringBuilder("Code,Company,Contact,Title,City,Country,Phone,Orders\n");
            foreach (var r in rows)
                csv.AppendJoin(',', new[] { r.Code, r.CompanyName, r.ContactName, r.ContactTitle, r.City, r.Country, r.Phone, r.OrderCount.ToString(CultureInfo.InvariantCulture) }
                    .Select(v => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\"")).Append('\n');
            return "data:text/csv;charset=utf-8," + Uri.EscapeDataString(csv.ToString());
        }
    }

    public void Dispose()
    {
        searchDebounce?.Cancel();
        searchDebounce?.Dispose();
    }
}
