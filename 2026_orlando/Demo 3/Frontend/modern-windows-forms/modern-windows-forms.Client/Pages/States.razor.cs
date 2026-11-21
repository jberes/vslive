using System.Globalization;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Services;

namespace modern_windows_forms.Client.Pages;

public partial class States
{
    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private ILogger<States> Logger { get; set; } = default!;

    private sealed record OrderPreview(int Id, string CustomerName, decimal Subtotal, string Status);
    private sealed record CustomerPreview(int Id, string CompanyName, int OrderCount);
    public sealed record ToastSample(string Kind, string Text, string? Action, string Icon);

    private static readonly ToastSample[] ToastSamples =
    [
        new("success", "Order #15245 marked as shipped", "Undo", "check-circle"),
        new("info", "Gantt rescheduled: 3 tasks moved", "View", "info"),
        new("warning", "Northwoods Cranberry Sauce: 4 units on backorder", null, "alert-triangle"),
        new("error", "Couldn’t save the task. Try again.", "Retry", "x-circle"),
    ];

    private static readonly (string Label, string Value, string Sub)[] KpiSkeletons =
        [("60%", "80%", "50%"), ("50%", "70%", "60%"), ("65%", "60%", "45%")];

    private static readonly string[] RowSkeletons = ["38%", "30%", "44%", "26%", "36%"];

    // Loading
    private bool loading = true;
    private List<OrderPreview> recentOrders = [];

    // Offline banner
    private bool offline = true;
    private string offlineSince = DateTime.Now.AddMinutes(-7).ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));

    // Empty search
    private string emptySearch = "gouda";

    // 409
    private IgbDialog? conflictDialog;
    private string conflictCompany = "Around the Horn";
    private int? conflictCustomerId;
    private int? conflictOrderCount;
    private int? conflictOpenOrders;

    // 404
    private int? notFoundStatus;
    private string? notFoundTitle;

    // 400 field validation
    private string email = "thomas@aroundthehorn";
    private string unitPrice = "0";
    private string companyName = "Around the Horn";
    private string postalCode = "WA1 1DP";

    // Toasts
    private IgbSnackbar? snackbar;
    private IgbToast? toast;
    private ToastSample? liveToast;

    private bool EmailInvalid => !System.Text.RegularExpressions.Regex.IsMatch(email ?? "", @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    private bool PriceInvalid => !decimal.TryParse(unitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) || p <= 0;
    private bool CompanyInvalid => string.IsNullOrWhiteSpace(companyName);

    /// <summary>The API's 409 message for deleting a customer with orders, plus live counts.</summary>
    private string ConflictMessage =>
        "Customer has orders and cannot be deleted." + (conflictOrderCount is { } n
            ? $" This account has {n} orders{(conflictOpenOrders is { } open ? $", {open} still open" : "")}."
            : "");

    private string ConflictOrdersHref => conflictCustomerId is { } id ? $"customers?search={Uri.EscapeDataString(conflictCompany)}" : "customers";

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadConflictCustomerAsync(), LoadNotFoundAsync());
    }

    private async Task LoadConflictCustomerAsync()
    {
        try
        {
            var page = await Api.GetAsync<PagedResult<CustomerPreview>>("api/customers?search=Around%20the%20Horn&pageSize=1");
            if (page.Items.FirstOrDefault() is { } c)
            {
                conflictCompany = c.CompanyName;
                conflictCustomerId = c.Id;
                conflictOrderCount = c.OrderCount;
                var open = await Api.GetAsync<PagedResult<OrderPreview>>($"api/orders?filter=customerId:eq:{c.Id}&filter=status:ne:Shipped&pageSize=1");
                conflictOpenOrders = open.TotalCount;
            }
        }
        catch (ApiException ex)
        {
            Logger.LogInformation("Conflict sample customer unavailable: {Title}", ex.Problem.Title);
        }
    }

    /// <summary>Calls a route that does not exist so the 404 panel shows the API's real ProblemDetails.</summary>
    private async Task LoadNotFoundAsync()
    {
        try
        {
            await Api.GetAsync<object>("api/orders/99999");
            notFoundStatus = 200;
            notFoundTitle = "OK";
        }
        catch (ApiException ex)
        {
            notFoundStatus = ex.Status;
            notFoundTitle = ex.IsNetworkError ? "no response" : ex.Problem.Title;
        }
    }

    private async Task ToggleLoadingAsync()
    {
        if (loading)
        {
            await FinishLoadingAsync();
        }
        else
        {
            loading = true;
        }
    }

    private async Task FinishLoadingAsync()
    {
        try
        {
            var page = await Api.GetAsync<PagedResult<OrderPreview>>("api/orders?pageSize=3&sort=-orderDate");
            recentOrders = [.. page.Items];
        }
        catch (ApiException ex)
        {
            await Notifications.ToastAsync($"Couldn't load orders. {ex.Problem.Title}", ToastKind.Error);
        }
        loading = false;
    }

    private async Task RetryAsync()
    {
        loading = true;
        StateHasChanged();
        await Task.Delay(600);
        await FinishLoadingAsync();
    }

    private async Task ReconnectAsync()
    {
        try
        {
            await Api.GetAsync<object>("health");
            offline = false;
        }
        catch (ApiException ex)
        {
            await Notifications.ToastAsync($"Still offline. {ex.Problem.Title}", ToastKind.Error);
        }
    }

    private async Task CopyDetailsAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", "GET /api/orders · net::ERR_CONNECTION_REFUSED");
            await Notifications.ToastAsync("Error details copied", ToastKind.Success);
        }
        catch (JSException)
        {
            await Notifications.ToastAsync("Couldn't access the clipboard", ToastKind.Error);
        }
    }

    private void ToggleEmptySearch() => emptySearch = string.IsNullOrEmpty(emptySearch) ? "gouda" : "";

    private Task OpenConflictAsync() => conflictDialog?.ShowAsync() ?? Task.CompletedTask;
    private Task CloseConflictAsync() => conflictDialog?.HideAsync() ?? Task.CompletedTask;

    private async Task ShowToastAsync(ToastSample sample)
    {
        liveToast = sample;
        StateHasChanged();
        if (sample.Action is not null)
        {
            if (toast is not null) await toast.HideAsync();
            if (snackbar is not null) await snackbar.ShowAsync();
        }
        else
        {
            if (snackbar is not null) await snackbar.HideAsync();
            if (toast is not null) await toast.ShowAsync();
        }
    }

    private async Task OnToastActionAsync(IgbVoidEventArgs _)
    {
        if (snackbar is not null) await snackbar.HideAsync();
        if (liveToast is not null)
            await Notifications.ToastAsync($"{liveToast.Action} clicked", ToastKind.Info);
    }
}
