using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Projects;
using modern_windows_forms.Client.Pages.ProjectSchedule;
using modern_windows_forms.Client.Services;
using modern_windows_forms.Client.Shared;

namespace modern_windows_forms.Client.Pages;

/// <summary>
/// 7 · Project schedule. ApexGantt (JS, via wwwroot/js/gantt-interop.js) draws the task list and timeline;
/// Ignite UI supplies the project picker, the ribbon-replacement toolbar and the task detail panel.
/// Every task mutation returns the refreshed gantt payload, which simply replaces local state.
/// </summary>
public partial class Projects
{
    public sealed record ZoomLevel(string Label, double PixelsPerDay);

    private static readonly ZoomLevel Day = new("Day", 48);
    private static readonly ZoomLevel Week = new("Week", 16);
    private static readonly ZoomLevel Month = new("Month", 5);
    private static readonly IReadOnlyList<ZoomLevel> ZoomLevels = [Day, Week, Month];
    private static readonly int[] PercentSteps = [0, 25, 50, 75, 100];

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private ILogger<Projects> Logger { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "id")] public int? RequestedProjectId { get; set; }

    private readonly DateTime today = DateTime.Today;
    private List<ProjectLookup> projects = [];
    private string? projectValue;
    private GanttPayload? gantt;
    private int? selectedTaskId;
    private ZoomLevel zoom = Week;
    private bool loading = true;
    private bool busy;
    private ProblemDetails? problem;

    private ElementReference ganttHost;
    private TaskDetailPanel? detailPanel;
    private DotNetObjectReference<Projects>? selfRef;
    private bool chartCreated;
    private bool chartDirty;
    private bool scrollToTodayOnRender;

    // ---------- Derived state ----------

    private GanttTask? SelectedTask => gantt?.Tasks.FirstOrDefault(t => t.Id == selectedTaskId);
    private bool HasSelection => SelectedTask is not null;
    private bool CanEdit => gantt is not null && !busy && !loading;
    private int? SelectedPercent => SelectedTask?.PercentComplete;
    private int OverallPercent => gantt is null ? 0 : (int)Math.Round(gantt.Project.PercentComplete);

    private string ProjectMeta
    {
        get
        {
            if (gantt is null) return string.Empty;
            var p = gantt.Project;
            var us = CultureInfo.GetCultureInfo("en-US");
            var dates = $"{p.StartDate.ToString("MMM d", us)} – {p.FinishDate.ToString("MMM d, yyyy", us)}";
            var parts = new List<string> { dates };
            if (!string.IsNullOrEmpty(p.ManagerName)) parts.Add(p.ManagerName);
            if (p.Budget is { } b) parts.Add(Format.Compact(b) + " budget");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>The "Link" button links the selected task (FS) to the next non-summary row below it.</summary>
    private GanttTask? LinkTarget
    {
        get
        {
            if (gantt is null || SelectedTask is not { IsSummary: false } sel) return null;
            var ordered = gantt.Tasks.OrderBy(t => t.RowNumber).ToList();
            var next = ordered.SkipWhile(t => t.Id != sel.Id).Skip(1).FirstOrDefault(t => !t.IsSummary);
            if (next is null) return null;
            var exists = gantt.Dependencies.Any(d => d.PredecessorId == sel.Id && d.SuccessorId == next.Id);
            return exists ? null : next;
        }
    }

    private string LinkTitle => LinkTarget is { } next && SelectedTask is { } sel
        ? $"Link {sel.Wbs} → {next.Wbs} (finish-to-start)"
        : "Link the selected task to the next task";

    // ---------- Lifecycle ----------

    protected override async Task OnInitializedAsync()
    {
        selfRef = DotNetObjectReference.Create(this);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        loading = true;
        problem = null;
        try
        {
            projects = (await Api.GetAsync<List<ProjectLookup>>("api/projects/lookup")).OrderBy(p => p.Name).ToList();
            var initialId = RequestedProjectId ?? await DefaultProjectIdAsync();
            if (initialId is null)
            {
                loading = false;
                return;
            }
            projectValue = initialId.Value.ToString();
            await LoadGanttAsync(initialId.Value, selectDefault: true);
            scrollToTodayOnRender = true;
        }
        catch (ApiException ex)
        {
            problem = ex.Problem;
            loading = false;
        }
    }

    /// <summary>The most recently started active project, else the first project.</summary>
    private async Task<int?> DefaultProjectIdAsync()
    {
        var list = await Api.GetAsync<PagedResult<ProjectDto>>("api/projects?pageSize=100&filter=status:eq:Active&sort=-startDate");
        return list.Items.FirstOrDefault()?.Id ?? projects.FirstOrDefault()?.Id;
    }

    private async Task LoadGanttAsync(int projectId, bool selectDefault)
    {
        loading = true;
        StateHasChanged();
        try
        {
            ApplyPayload(await Api.GetAsync<GanttPayload>($"api/projects/{projectId}/gantt"), selectDefault);
        }
        catch (ApiException ex)
        {
            problem = ex.Problem;
        }
        finally
        {
            loading = false;
        }
    }

    private void ApplyPayload(GanttPayload payload, bool selectDefault = false, int? select = null)
    {
        gantt = payload;
        if (select is not null) selectedTaskId = select;
        if (selectDefault || SelectedTask is null) selectedTaskId = DefaultSelection(payload)?.Id;
        chartDirty = true;
    }

    /// <summary>Open on the task that needs attention: first leaf that is behind, else the first one in progress.</summary>
    private GanttTask? DefaultSelection(GanttPayload payload)
    {
        var leaves = payload.Tasks.Where(t => !t.IsSummary).OrderBy(t => t.RowNumber).ToList();
        return leaves.FirstOrDefault(t => ScheduleMath.StatusOf(t, today) == ScheduleStatus.Behind)
               ?? leaves.FirstOrDefault(t => ScheduleMath.StatusOf(t, today) == ScheduleStatus.InProgress)
               ?? payload.Tasks.OrderBy(t => t.RowNumber).FirstOrDefault();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!chartDirty || gantt is null || loading) return;
        chartDirty = false;
        var state = new
        {
            ppd = zoom.PixelsPerDay,
            today = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            selectedId = selectedTaskId,
            scrollToToday = scrollToTodayOnRender
        };
        try
        {
            if (!chartCreated)
            {
                await JS.InvokeVoidAsync("ganttInterop.render", ganttHost, selfRef, gantt, state);
                chartCreated = true;
            }
            else
            {
                await JS.InvokeVoidAsync("ganttInterop.update", ganttHost, gantt, state);
                if (scrollToTodayOnRender) await JS.InvokeVoidAsync("ganttInterop.scrollToToday", ganttHost);
            }
            scrollToTodayOnRender = false;
        }
        catch (JSException ex)
        {
            Logger.LogError(ex, "ApexGantt failed to render");
            problem = new ProblemDetails { Title = "The schedule chart couldn't load.", Detail = "ApexGantt didn't load from the CDN. Check the network, then retry.", Status = 0 };
            StateHasChanged();
        }
    }

    // ---------- Callbacks from ApexGantt (gantt-interop.js) ----------

    [JSInvokable]
    public Task OnGanttTaskSelected(int taskId)
    {
        if (selectedTaskId == taskId) return Task.CompletedTask;
        selectedTaskId = taskId;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnGanttProgressDragged(int taskId, int percent)
    {
        selectedTaskId = taskId;
        return MutateAsync(() => Api.PatchAsync<GanttPayload>($"api/tasks/{taskId}/progress", new { percentComplete = Math.Clamp(percent, 0, 100) }),
            $"Progress set to {Math.Clamp(percent, 0, 100)}%");
    }

    // ---------- Toolbar ----------

    private async Task OnProjectChangedAsync()
    {
        if (!int.TryParse(projectValue, out var id) || id == gantt?.Project.Id) return;
        problem = null;
        await LoadGanttAsync(id, selectDefault: true);
        scrollToTodayOnRender = true;
    }

    private Task InsertTaskAsync() => InsertAsync(milestone: false);
    private Task InsertMilestoneAsync() => InsertAsync(milestone: true);

    private async Task InsertAsync(bool milestone)
    {
        if (gantt is null) return;
        var after = SelectedTask;
        var body = new
        {
            name = milestone ? "New milestone" : "New task",
            afterTaskId = after is { IsSummary: false } ? after.Id : (int?)null,
            parentId = after is { IsSummary: true } ? after.Id : (int?)null,
            durationDays = milestone ? 0 : 5,
            isMilestone = milestone
        };
        busy = true;
        try
        {
            var created = await Api.PostAsync<TaskCreated>($"api/projects/{gantt.Project.Id}/tasks", body);
            ApplyPayload(created.Gantt, select: created.TaskId);
            var where = after is null ? "at the end" : after.IsSummary ? $"under {after.Wbs} {after.Name}" : $"below {after.Wbs}";
            await Notifications.ToastAsync($"{(milestone ? "Milestone" : "Task")} inserted {where}", ToastKind.Success);
        }
        catch (ApiException ex)
        {
            await ReportAsync(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private Task IndentAsync() => TaskCommandAsync("indent", null, "Indented");
    private Task OutdentAsync() => TaskCommandAsync("outdent", null, "Outdented");
    private Task MoveUpAsync() => TaskCommandAsync("move", new { direction = "up" }, "Moved up");
    private Task MoveDownAsync() => TaskCommandAsync("move", new { direction = "down" }, "Moved down");

    private Task TaskCommandAsync(string command, object? body, string done)
    {
        if (SelectedTask is not { } t) return Task.CompletedTask;
        return MutateAsync(() => Api.PostAsync<GanttPayload>($"api/tasks/{t.Id}/{command}", body ?? new { }), $"{done}: {t.Name}");
    }

    private Task SetProgressAsync(int percent)
    {
        if (SelectedTask is not { } t || t.PercentComplete == percent && !t.IsSummary) return Task.CompletedTask;
        return MutateAsync(() => Api.PatchAsync<GanttPayload>($"api/tasks/{t.Id}/progress", new { percentComplete = percent }),
            t.IsSummary ? $"{t.Name}: all tasks set to {percent}%" : $"{t.Name} set to {percent}%");
    }

    private async Task LinkToNextAsync()
    {
        if (gantt is null || SelectedTask is not { } from || LinkTarget is not { } to) return;
        busy = true;
        try
        {
            await Api.PostAsync<GanttDependency>($"api/projects/{gantt.Project.Id}/dependencies",
                new { predecessorId = from.Id, successorId = to.Id, type = "FinishToStart", lagDays = 0 });
            ApplyPayload(await Api.GetAsync<GanttPayload>($"api/projects/{gantt.Project.Id}/gantt"));
            await Notifications.ToastAsync($"Linked {from.Wbs} → {to.Wbs} (finish-to-start)", ToastKind.Success);
        }
        catch (ApiException ex)
        {
            await ReportAsync(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task FocusNotes()
    {
        if (detailPanel is not null) await detailPanel.FocusNotesAsync();
    }

    private async Task SetZoomAsync(ZoomLevel level)
    {
        zoom = level;
        if (chartCreated) await JS.InvokeVoidAsync("ganttInterop.setZoom", ganttHost, level.PixelsPerDay);
    }

    private async Task ScrollToTodayAsync()
    {
        if (chartCreated) await JS.InvokeVoidAsync("ganttInterop.scrollToToday", ganttHost);
    }

    private Task RetryAsync()
    {
        problem = null;
        return gantt is null || !int.TryParse(projectValue, out var id) ? LoadAsync() : LoadGanttAsync(id, selectDefault: false);
    }

    // ---------- Detail panel edits ----------

    private Task OnDatesChangedAsync(TaskDetailPanel.DateEdit edit)
    {
        if (SelectedTask is not { IsSummary: false } t) return Task.CompletedTask;
        var duration = t.IsMilestone ? 0 : Math.Max(0, (edit.Finish - edit.Start).TotalDays);
        var body = new
        {
            name = t.Name,
            start = edit.Start,
            durationDays = duration,
            percentComplete = t.PercentComplete,
            isMilestone = t.IsMilestone,
            notes = t.Notes
        };
        return MutateAsync(() => Api.PutAsync<GanttPayload>($"api/tasks/{t.Id}", body), $"{t.Name} rescheduled; dependent tasks updated");
    }

    private Task OnNotesCommittedAsync(string? notes)
    {
        if (SelectedTask is not { } t || (t.Notes ?? string.Empty) == (notes ?? string.Empty)) return Task.CompletedTask;
        return MutateAsync(() => Api.PutAsync<GanttPayload>($"api/tasks/{t.Id}/notes", new { notes }), "Notes saved");
    }

    // ---------- Helpers ----------

    private async Task MutateAsync(Func<Task<GanttPayload>> call, string successMessage)
    {
        busy = true;
        StateHasChanged();
        try
        {
            ApplyPayload(await call());
            await Notifications.ToastAsync(successMessage, ToastKind.Success);
        }
        catch (ApiException ex)
        {
            await ReportAsync(ex);
            chartDirty = true; // re-render from server state (undoes an optimistic progress drag)
        }
        finally
        {
            busy = false;
            StateHasChanged();
        }
    }

    private Task ReportAsync(ApiException ex)
    {
        if (ex.IsNetworkError)
        {
            problem = ex.Problem;
            return Task.CompletedTask;
        }
        var message = ex.Problem.Errors?.Values.SelectMany(v => v).FirstOrDefault()
                      ?? ex.Problem.Detail ?? ex.Problem.Title ?? "The change couldn't be saved.";
        return Notifications.ToastAsync(message, ToastKind.Error);
    }

    public async ValueTask DisposeAsync()
    {
        if (chartCreated)
        {
            try
            {
                await JS.InvokeVoidAsync("ganttInterop.destroy", ganttHost);
            }
            catch (JSDisconnectedException) { }
            catch (JSException ex)
            {
                Logger.LogDebug(ex, "ApexGantt destroy failed");
            }
        }
        selfRef?.Dispose();
    }
}
