using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using IgniteUI.Blazor.Controls;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Services;
using modern_windows_forms.Client.Shared;

namespace modern_windows_forms.Client.Layout;

public partial class MainLayout
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Logger { get; set; } = default!;

    private bool drawerOpen = true;
    private string activeKey = "home";
    private string section = "Home";
    private string pageLabel = "Home";
    private string searchText = string.Empty;
    private ErrorBoundary? errorBoundary;

    private IgbToast? toast;
    private IgbSnackbar? snackbar;
    private string toastMessage = string.Empty;
    private ToastKind toastKind;
    private string snackMessage = string.Empty;
    private string snackAction = "Undo";
    private Func<Task>? snackCallback;

    private DashboardSummary? summary;

    protected override async Task OnInitializedAsync()
    {
        Nav.LocationChanged += OnLocationChanged;
        Notifications.Raised += OnNotification;
        SyncActive(Nav.Uri);
        try
        {
            summary = await Api.GetAsync<DashboardSummary>("api/dashboard/summary");
        }
        catch (ApiException ex)
        {
            Logger.LogWarning("Nav badges unavailable: {Title}", ex.Problem.Title);
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        SyncActive(e.Location);
        errorBoundary?.Recover();
        StateHasChanged();
    }

    private void SyncActive(string uri)
    {
        var item = NavModel.Match(Nav.ToBaseRelativePath(uri));
        activeKey = item?.Key ?? string.Empty;
        section = item?.Section ?? string.Empty;
        pageLabel = item?.Label ?? "Not found";
    }

    private string ItemClass(NavItem item) => item.Key == activeKey ? "nav-item nav-item--on" : "nav-item";

    private bool IsPhoneTabActive(NavItem tab) =>
        tab.Key == activeKey || (tab.Key == "more" && activeKey is not ("home" or "orders" or "projects" or "leads"));

    private (string Text, BadgeTone Tone)? BadgeFor(string key) => summary is null ? null : key switch
    {
        "customers" => (Format.Number(summary.Sales.TotalCustomers), BadgeTone.Neutral),
        "orders" when summary.Sales.OverdueShipments > 0 => (summary.Sales.OverdueShipments.ToString(), BadgeTone.Error),
        "projects" when summary.Projects.OverdueTasks > 0 => (summary.Projects.OverdueTasks.ToString(), BadgeTone.Error),
        "leads" => (Format.Number(summary.Marketing.OpenLeads), BadgeTone.Neutral),
        "campaigns" => (summary.Marketing.ActiveCampaigns.ToString(), BadgeTone.Neutral),
        _ => null
    };

    private void Go(NavItem item) => Nav.NavigateTo(item.Href);

    private void ToggleDrawer() => drawerOpen = !drawerOpen;

    private void OnSearchKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(searchText))
            Nav.NavigateTo("customers?search=" + Uri.EscapeDataString(searchText.Trim()));
    }

    private void Recover() => errorBoundary?.Recover();

    private string ToastClass => toastKind switch
    {
        ToastKind.Success => "app-toast app-toast--success",
        ToastKind.Error => "app-toast app-toast--error",
        _ => "app-toast"
    };

    private async Task OnNotification(AppNotification n)
    {
        await InvokeAsync(async () =>
        {
            if (n.ActionText is not null && snackbar is not null)
            {
                snackMessage = n.Message;
                snackAction = n.ActionText;
                snackCallback = n.Action;
                StateHasChanged();
                await snackbar.ShowAsync();
            }
            else if (toast is not null)
            {
                toastMessage = n.Message;
                toastKind = n.Kind;
                StateHasChanged();
                await toast.ShowAsync();
            }
        });
    }

    private async Task OnSnackAction(IgbVoidEventArgs _)
    {
        if (snackbar is not null) await snackbar.HideAsync();
        if (snackCallback is not null) await snackCallback();
    }

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
        Notifications.Raised -= OnNotification;
    }
}
