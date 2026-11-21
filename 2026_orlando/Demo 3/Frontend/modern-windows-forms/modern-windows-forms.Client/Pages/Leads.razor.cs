using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Marketing;
using modern_windows_forms.Client.Pages.Marketing;
using modern_windows_forms.Client.Services;

namespace modern_windows_forms.Client.Pages;

public partial class Leads
{
    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private const int PageSize = 20;
    private const string AllOwners = "all";

    private static readonly LeadStatus[] OpenStages = [LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Proposal, LeadStatus.Won];

    private sealed class Column
    {
        public List<LeadDto> Items { get; set; } = [];
        public bool Loading { get; set; } = true;
        public bool Loaded { get; set; }
        public int Shown { get; set; } = PageSize;
    }

    private readonly Dictionary<LeadStatus, Column> columns = Enum.GetValues<LeadStatus>().ToDictionary(s => s, _ => new Column());

    private MarketingKpis? kpis;
    private int? lostCount;
    private bool showLost;
    private string ownerFilter = AllOwners;
    private ProblemDetails? loadError;

    private LeadDto? dragLead;
    private LeadStatus? overStatus;
    private ConvertLeadDialog? convertDialog;

    private IEnumerable<LeadStatus> VisibleStatuses => showLost ? [.. OpenStages, LeadStatus.Lost] : OpenStages;

    /// <summary>Owners present in the loaded leads, for the owner IgbSelect.</summary>
    private IEnumerable<(int Id, string Name)> Owners => columns.Values
        .SelectMany(c => c.Items)
        .Where(l => l.OwnerId is not null)
        .Select(l => (Id: l.OwnerId!.Value, Name: l.OwnerName ?? $"#{l.OwnerId}"))
        .Distinct()
        .OrderBy(o => o.Name);

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        loadError = null;
        foreach (var c in columns.Values) { c.Loading = true; c.Loaded = false; c.Shown = PageSize; }
        try
        {
            var dashboardTask = Api.GetAsync<MarketingDashboardWithStatus>("api/marketing/dashboard?months=12");
            var columnTasks = OpenStages.Select(LoadColumnAsync).ToList();
            var dashboard = await dashboardTask;
            kpis = dashboard.Kpis;
            lostCount = dashboard.LeadsByStatus.FirstOrDefault(s => s.Name == nameof(LeadStatus.Lost))?.Value is { } lost ? (int)lost : null;
            await Task.WhenAll(columnTasks);
            if (showLost) await LoadColumnAsync(LeadStatus.Lost);
        }
        catch (ApiException ex)
        {
            loadError = ex.Problem;
            foreach (var c in columns.Values) c.Loading = false;
        }
    }

    /// <summary>
    /// Loads one stage (≤ 1,000 leads, sorted newest first). Whole stages are fetched so the column count and
    /// estimated value are exact and owner filtering is instant; cards render 20 at a time ("Show N more").
    /// </summary>
    private async Task LoadColumnAsync(LeadStatus status)
    {
        var column = columns[status];
        column.Loading = true;
        var page = await Api.GetAsync<PagedResult<LeadDto>>($"api/leads?filter=status:eq:{status}&sort=-createdAt&pageSize=1000");
        column.Items = [.. page.Items];
        column.Loading = false;
        column.Loaded = true;
        StateHasChanged();
    }

    private List<LeadDto> Filtered(Column column) =>
        ownerFilter == AllOwners ? column.Items : column.Items.Where(l => l.OwnerId?.ToString() == ownerFilter).ToList();

    private async Task ToggleLostAsync()
    {
        showLost = !showLost;
        if (showLost && !columns[LeadStatus.Lost].Loaded)
        {
            try { await LoadColumnAsync(LeadStatus.Lost); }
            catch (ApiException ex)
            {
                columns[LeadStatus.Lost].Loading = false;
                await Notifications.ToastAsync($"Couldn't load lost leads. {ex.Problem.Title}", ToastKind.Error);
            }
        }
    }

    private Task NewLeadAsync() =>
        Notifications.ToastAsync("New lead entry isn't part of this prototype yet.");

    // ---------- Drag and drop ----------

    private void StartDrag(LeadDto lead) => dragLead = lead;

    private void EndDrag()
    {
        dragLead = null;
        overStatus = null;
    }

    private async Task DropAsync(LeadStatus to)
    {
        var lead = dragLead;
        EndDrag();
        if (lead is not null) await MoveAsync(lead, to, offerUndo: true);
    }

    private Task AdvanceAsync((LeadDto Lead, LeadStatus To) move) => MoveAsync(move.Lead, move.To, offerUndo: true);

    /// <summary>
    /// Optimistically moves a card, then PATCHes /api/leads/{id}/status. Moving an unconverted lead to Won opens
    /// the convert dialog instead. Failures revert the card and raise an error toast.
    /// </summary>
    private async Task MoveAsync(LeadDto lead, LeadStatus to, bool offerUndo)
    {
        var from = lead.Status;
        if (from == to) return;

        if (to == LeadStatus.Won && lead.ConvertedCustomerId is null)
        {
            if (convertDialog is not null) await convertDialog.OpenAsync(lead);
            return;
        }

        var moved = lead with { Status = to };
        Place(lead, moved);
        try
        {
            var saved = await Api.PatchAsync<LeadDto>($"api/leads/{lead.Id}/status", new LeadStatusInput(to));
            Place(moved, saved);
            if (offerUndo)
                await Notifications.SnackbarAsync($"{lead.FullName} moved to {to}", "Undo",
                    () => InvokeAsync(() => MoveAsync(saved, from, offerUndo: false)));
        }
        catch (ApiException ex)
        {
            Place(moved, lead);
            await Notifications.ToastAsync($"Couldn't move {lead.FullName}. {ex.Problem.Detail ?? ex.Problem.Title}", ToastKind.Error);
        }
        StateHasChanged();
    }

    /// <summary>Removes <paramref name="old"/> from its column and inserts <paramref name="updated"/> at the top of its new column.</summary>
    private void Place(LeadDto old, LeadDto updated)
    {
        columns[old.Status].Items.RemoveAll(l => l.Id == old.Id);
        var target = columns[updated.Status];
        target.Items.RemoveAll(l => l.Id == updated.Id);
        target.Items.Insert(0, updated);
    }

    // ---------- Conversion ----------

    private async Task OnConvertedAsync(LeadConversion result)
    {
        var existing = columns.Values.SelectMany(c => c.Items).FirstOrDefault(l => l.Id == result.Lead.Id);
        if (existing is not null) Place(existing, result.Lead);
        else columns[LeadStatus.Won].Items.Insert(0, result.Lead);
        if (kpis is not null) kpis = kpis with { OpenLeads = Math.Max(0, kpis.OpenLeads - 1), WonLeads = kpis.WonLeads + 1 };
        StateHasChanged();

        var code = result.Customer.Code;
        await Notifications.SnackbarAsync(
            $"{result.Lead.FullName} converted · {result.Customer.CompanyName} is now customer {code}",
            "Open customer",
            () => { Nav.NavigateTo("customers?search=" + Uri.EscapeDataString(code)); return Task.CompletedTask; });
    }

    private void OnConvertCancelled(LeadDto lead) => StateHasChanged();

    private static string StageColor(LeadStatus status) => status switch
    {
        LeadStatus.New => "var(--chart-1)",
        LeadStatus.Contacted => "var(--chart-accent-teal)",
        LeadStatus.Qualified => "var(--chart-5)",
        LeadStatus.Proposal => "var(--chart-3)",
        LeadStatus.Won => "var(--color-success)",
        _ => "var(--color-text-tertiary)"
    };

    /// <summary>Marketing dashboard KPIs plus the per-status counts (used for "Show lost (849)").</summary>
    private sealed record MarketingDashboardWithStatus(MarketingKpis Kpis, IReadOnlyList<NameValue> LeadsByStatus);
}
