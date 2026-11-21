using System.Globalization;
using System.Text.RegularExpressions;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Orders;
using modern_windows_forms.Client.Services;
using Format = modern_windows_forms.Client.Shared.Format;

namespace modern_windows_forms.Client.Pages;

public partial class NewOrder
{
    private const int DefaultEmployeeId = 8; // Laura Callahan, the signed-in user in the design

    private static readonly (string Value, string Label)[] Discounts =
        [("0", "0%"), ("0.05", "5%"), ("0.1", "10%"), ("0.15", "15%"), ("0.2", "20%"), ("0.25", "25%")];

    private static readonly Regex LineKey = new(@"^details\[(\d+)\]\.(\w+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;

    private List<CustomerLookup> customers = [];
    private List<EmployeeLookup> employees = [];
    private List<Shipper> shippers = [];
    private List<ProductLookup> products = [];
    private ProblemDetails? loadError;

    // Form state
    private int? customerId;
    private CustomerDetail? customer;
    private string employeeValue = DefaultEmployeeId.ToString(CultureInfo.InvariantCulture);
    private DateTime? orderDate = DateTime.Today;
    private DateTime? requiredDate = DateTime.Today.AddDays(28);
    private string shipViaValue = "1";
    private string freightText = "0.00";
    private List<NewOrderLine> lines = [new()];

    // Submit state
    private bool submitting;
    private ProblemDetails? problem;
    private List<FieldError> errors = [];
    private OrderRow? created;
    private int createdLineCount;
    private string createdBackorderNote = string.Empty;

    protected override Task OnInitializedAsync() => LoadReferenceDataAsync();

    private async Task LoadReferenceDataAsync()
    {
        try
        {
            var customerTask = Api.GetAsync<List<CustomerLookup>>("api/customers/lookup");
            var employeeTask = Api.GetAsync<List<EmployeeLookup>>("api/employees/lookup");
            var shipperTask = Api.GetAsync<PagedResult<Shipper>>("api/shippers?sort=id");
            // Discontinued products are listed (marked) so the server's 400 for them can be shown on the line.
            var productTask = Api.GetAsync<List<ProductLookup>>("api/products/lookup?includeDiscontinued=true");
            await Task.WhenAll(customerTask, employeeTask, shipperTask, productTask);
            customers = customerTask.Result;
            employees = employeeTask.Result;
            shippers = [.. shipperTask.Result.Items];
            products = productTask.Result;
            if (employees.All(e => e.Id != DefaultEmployeeId) && employees.Count > 0)
                employeeValue = employees[0].Id.ToString(CultureInfo.InvariantCulture);
            loadError = null;
        }
        catch (ApiException ex)
        {
            loadError = ex.Problem;
        }
    }

    // ---------- Customer ----------

    // IgbCombo 26.1.98 stops rendering when its Value parameter is set (bound or not), so the combos are
    // uncontrolled: selection is read from the Change event instead of Value/ValueChanged.
    private Task OnCustomerComboChange(IgbComboChangeEventArgs e) => OnCustomerChanged(SelectedIds(e));

    private void OnProductComboChange(NewOrderLine line, IgbComboChangeEventArgs e) => OnProductChanged(line, SelectedIds(e));

    private static int[] SelectedIds(IgbComboChangeEventArgs e) =>
        (e.Detail?.NewValue ?? []).Select(ToId).OfType<int>().ToArray();

    private static int? ToId(object? value) => value switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } j => j.GetInt32(),
        string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
        _ => null
    };

    private async Task OnCustomerChanged(int[] value)
    {
        customerId = value.Length > 0 ? value[0] : null;
        customer = null;
        ClearError("customerId");
        if (customerId is not int id) return;
        try
        {
            customer = await Api.GetAsync<CustomerDetail>($"api/customers/{id}");
        }
        catch (ApiException)
        {
            customer = null; // the meta line falls back to the lookup row
        }
    }

    private string CustomerMeta
    {
        get
        {
            if (customer is not null)
                return string.Join(" · ", new[] { customer.ContactName, Place(customer.City, customer.Country), $"{customer.OrderCount} orders" }
                    .Where(s => !string.IsNullOrEmpty(s)));
            var lookup = customers.FirstOrDefault(c => c.Id == customerId);
            return lookup is null ? "Search by company name or customer code" : $"{lookup.Code} · {Place(lookup.City, lookup.Country)}";
        }
    }

    private string CustomerAddress => customer is null ? string.Empty
        : string.Join(", ", new[] { customer.Address, string.Join(" ", new[] { customer.City, customer.PostalCode }.Where(s => !string.IsNullOrEmpty(s))), customer.Country }
            .Where(s => !string.IsNullOrEmpty(s)));

    private static string Place(string? city, string? country) =>
        string.Join(", ", new[] { city, country }.Where(s => !string.IsNullOrEmpty(s)));

    // ---------- Dates ----------

    private DateTime DefaultRequiredDate => (orderDate ?? DateTime.Today).Date.AddDays(28);

    private void OnOrderDateChanged(DateTime? value)
    {
        var keepDefault = requiredDate == DefaultRequiredDate;
        orderDate = value;
        if (keepDefault) requiredDate = DefaultRequiredDate;
    }

    private void UseDefaultRequiredDate()
    {
        requiredDate = DefaultRequiredDate;
        ClearError("requiredDate");
    }

    // ---------- Ship via ----------

    private string ShipCardClass(Shipper s) => s.Id.ToString(CultureInfo.InvariantCulture) == shipViaValue ? "ship-card ship-card--on" : "ship-card";

    // ---------- Lines ----------

    private ProductLookup? ProductFor(NewOrderLine line) => line.ProductId is int id ? products.FirstOrDefault(p => p.Id == id) : null;

    private void OnProductChanged(NewOrderLine line, int[] value)
    {
        line.ProductId = value.Length > 0 ? value[0] : null;
        ClearError("details");
    }

    private void AddLine() => lines.Add(new NewOrderLine());

    private void RemoveLine(NewOrderLine line) => lines.Remove(line);

    private decimal LineTotal(NewOrderLine line) =>
        ProductFor(line) is { } p ? Math.Round(p.UnitPrice * line.Quantity * (decimal)(1 - line.DiscountValue), 2) : 0m;

    private decimal Subtotal => lines.Sum(LineTotal);
    private int Units => lines.Where(l => l.ProductId is not null).Sum(l => Math.Max(0, l.Quantity));

    private bool FreightValid => decimal.TryParse(freightText, NumberStyles.Number, CultureInfo.InvariantCulture, out var f) && f >= 0;
    private decimal Freight => FreightValid ? decimal.Parse(freightText, NumberStyles.Number, CultureInfo.InvariantCulture) : 0m;

    private List<(ProductLookup Product, int Short)> Backorders =>
        lines.Select(l => (Line: l, Product: ProductFor(l)))
             .Where(x => x.Product is { Discontinued: false } p && x.Line.Quantity > p.UnitsInStock)
             .Select(x => (x.Product!, x.Line.Quantity - Math.Max(0, x.Product!.UnitsInStock)))
             .ToList();

    private string BackorderNote
    {
        get
        {
            var items = Backorders;
            return items.Count == 0 ? "No backorders."
                : string.Join(" · ", items.Select(b => $"{b.Product.Name}: {b.Short} on backorder ({Math.Max(0, b.Product.UnitsInStock)} in stock)"));
        }
    }

    /// <summary>A line is flagged when the server rejected it (indexed error, or named in the discontinued message).</summary>
    private bool LineInvalid(int index, NewOrderLine line, ProductLookup? product) =>
        LineError(index, null) is not null || (product is not null && DetailsErrorMentions(product.Name));

    private bool DetailsErrorMentions(string productName) =>
        ErrorFor("details") is { } msg && msg.Contains(productName, StringComparison.OrdinalIgnoreCase);

    private string LineRowClass(int index, NewOrderLine line, ProductLookup? product) =>
        LineInvalid(index, line, product) ? "lines__row lines__row--error" : "lines__row";

    private string StockNote(int index, NewOrderLine line, ProductLookup? product)
    {
        if (LineError(index, null) is { } err) return err;
        if (product is null) return "Pick a product";
        if (product.Discontinued) return "Discontinued · not orderable";
        if (line.Quantity > product.UnitsInStock) return $"Backorder: {Math.Max(0, product.UnitsInStock)} in stock";
        return $"{product.UnitsInStock} in stock";
    }

    private string StockNoteClass(int index, NewOrderLine line, ProductLookup? product)
    {
        if (LineInvalid(index, line, product)) return "stock-note stock-note--error";
        if (product is { Discontinued: true } || (product is not null && line.Quantity > product.UnitsInStock)) return "stock-note stock-note--warn";
        return "stock-note";
    }

    // ---------- Errors (RFC 7807 400 → fields) ----------

    private string? ErrorFor(string field) =>
        errors.FirstOrDefault(e => string.Equals(e.Field, field, StringComparison.OrdinalIgnoreCase))?.Message;

    private bool HasError(string field) => ErrorFor(field) is not null;

    /// <summary>Error for <c>Details[i].Field</c> (or any field on that line when <paramref name="field"/> is null).</summary>
    private string? LineError(int index, string? field) => errors
        .Select(e => (Match: LineKey.Match(e.Field), e.Message))
        .FirstOrDefault(x => x.Match.Success && int.Parse(x.Match.Groups[1].Value, CultureInfo.InvariantCulture) == index
                             && (field is null || string.Equals(x.Match.Groups[2].Value, field, StringComparison.OrdinalIgnoreCase)))
        .Message;

    private void ClearError(string field)
    {
        if (errors.RemoveAll(e => string.Equals(e.Field, field, StringComparison.OrdinalIgnoreCase)) > 0 && errors.Count == 0)
            problem = null;
    }

    // ---------- Submit ----------

    private async Task SubmitAsync()
    {
        if (submitting) return;
        submitting = true;
        problem = null;
        errors = [];

        var submitted = lines.ToList();
        var input = new OrderInput
        {
            CustomerId = customerId ?? 0,
            EmployeeId = int.TryParse(employeeValue, out var emp) ? emp : 0,
            ShipViaId = int.TryParse(shipViaValue, out var via) ? via : 0,
            OrderDate = BusinessDate.From(orderDate),
            RequiredDate = BusinessDate.From(requiredDate),
            Freight = Freight,
            // Validation (product, quantity, discontinued, stock) is the server's job; its 400 is mapped back onto the form.
            Details = submitted.Select(l => new OrderLineInput(l.ProductId ?? 0, l.Quantity, null, l.DiscountValue)).ToList()
        };

        try
        {
            var backorderNote = BackorderNote;
            created = await Api.PostAsync<OrderRow>("api/orders", input);
            createdLineCount = submitted.Count;
            createdBackorderNote = backorderNote;
            await Notifications.ToastAsync($"Order #{created.Id} created", ToastKind.Success);
        }
        catch (ApiException ex)
        {
            problem = ex.Problem;
            errors = ex.Problem.Errors?
                .SelectMany(kv => kv.Value.Select(msg => new FieldError(kv.Key, msg)))
                .ToList() ?? [];
        }
        finally
        {
            submitting = false;
        }
    }

    private Task ResetAsync()
    {
        created = null;
        problem = null;
        errors = [];
        customerId = null;
        customer = null;
        employeeValue = DefaultEmployeeId.ToString(CultureInfo.InvariantCulture);
        orderDate = DateTime.Today;
        requiredDate = DateTime.Today.AddDays(28);
        shipViaValue = "1";
        freightText = "0.00";
        lines = [new()];
        return Task.CompletedTask;
    }
}
