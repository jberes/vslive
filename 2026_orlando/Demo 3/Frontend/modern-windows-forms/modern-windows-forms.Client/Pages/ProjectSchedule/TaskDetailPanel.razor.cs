using System.Globalization;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models.Projects;
using modern_windows_forms.Client.Shared;

namespace modern_windows_forms.Client.Pages.ProjectSchedule;

public partial class TaskDetailPanel
{
    public sealed record DateEdit(DateTime Start, DateTime Finish);
    private sealed record Person(string Name, string Role);

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    [Parameter] public GanttTask? Current { get; set; }
    [Parameter] public GanttPayload? Gantt { get; set; }
    [Parameter] public DateTime Today { get; set; } = DateTime.Today;
    [Parameter] public bool Loading { get; set; }
    [Parameter] public bool Busy { get; set; }
    [Parameter] public EventCallback<DateEdit> DatesChanged { get; set; }
    [Parameter] public EventCallback<string?> NotesCommitted { get; set; }

    private IgbTextarea? notesArea;
    private string? notesDraft;
    private int? notesTaskId;
    private string? notesSource;

    private ScheduleStatus Status => Current is null ? ScheduleStatus.NotStarted
        : Current.IsSummary && Gantt is not null
            ? ScheduleMath.PhaseStatus(Current, Leaves(Current.Id), Today)
            : ScheduleMath.StatusOf(Current, Today);

    private bool DatesReadOnly => Current is null || Current.IsSummary || Busy;

    private string ProgressClass => Current?.PercentComplete >= 100 ? "detail-bar lp--green" : "detail-bar";

    private string? BehindText
    {
        get
        {
            if (Current is null) return null;
            if (Current.IsSummary)
            {
                var late = Leaves(Current.Id).Where(l => ScheduleMath.StatusOf(l, Today) == ScheduleStatus.Behind).Select(l => l.Name).ToList();
                return late.Count == 0 ? null : $"Behind schedule: {string.Join(", ", late)} {(late.Count == 1 ? "is" : "are")} behind.";
            }
            if (Current.IsMilestone) return $"Milestone was due {Current.Start.ToString("MMM d", Us)} and isn't complete.";
            var expected = ScheduleMath.ExpectedPercent(Current, Today) ?? 0;
            var elapsed = Math.Clamp((int)Math.Floor((Today - Current.Start).TotalDays), 0, (int)Math.Round(Current.DurationDays));
            return $"Behind schedule: started {Current.Start.ToString("MMM d", Us)}, {elapsed} of {Math.Round(Current.DurationDays)} days elapsed. Expected about {expected}% by today.";
        }
    }

    private List<Person> People
    {
        get
        {
            if (Current is null) return [];
            if (Current.Assignments.Count == 0)
            {
                return Current.IsSummary && !string.IsNullOrEmpty(Gantt?.Project.ManagerName)
                    ? [new Person(Gantt.Project.ManagerName!, "Project manager (summary owner)")]
                    : [];
            }
            return Current.Assignments.Select(a =>
            {
                var r = Gantt?.Resources.FirstOrDefault(x => x.Id == a.ResourceId);
                var role = r?.Role ?? "Resource";
                if (r?.HourlyRate is { } rate) role += $" · {rate.ToString("C0", Us)}/h";
                if (a.Units != 100) role += $" · {a.Units}%";
                return new Person(a.ResourceName, role);
            }).ToList();
        }
    }

    private string DependsOn => DescribeLinks(incoming: true);
    private string Blocks => DescribeLinks(incoming: false);

    private string DescribeLinks(bool incoming)
    {
        if (Current is null || Gantt is null) return "None";
        var links = Gantt.Dependencies.Where(d => incoming ? d.SuccessorId == Current.Id : d.PredecessorId == Current.Id)
            .Select(d =>
            {
                var other = Gantt.Tasks.FirstOrDefault(t => t.Id == (incoming ? d.PredecessorId : d.SuccessorId));
                var lag = d.LagDays == 0 ? string.Empty : $" {(d.LagDays > 0 ? "+" : "−")}{Math.Abs(d.LagDays):0.#}d";
                return other is null ? null : $"{other.Wbs} {other.Name} ({d.TypeCode}{lag})";
            })
            .OfType<string>()
            .ToList();
        return links.Count == 0 ? "None" : string.Join(", ", links);
    }

    private IEnumerable<GanttTask> Leaves(int summaryId)
    {
        if (Gantt is null) yield break;
        var children = Gantt.Tasks.ToLookup(t => t.ParentId);
        var stack = new Stack<int>([summaryId]);
        while (stack.Count > 0)
        {
            foreach (var c in children[stack.Pop()])
            {
                if (c.IsSummary) stack.Push(c.Id);
                else yield return c;
            }
        }
    }

    protected override void OnParametersSet()
    {
        // Reset the notes draft when the selection changes or the server copy changes.
        if (Current?.Id != notesTaskId || Current?.Notes != notesSource)
        {
            notesTaskId = Current?.Id;
            notesSource = Current?.Notes;
            notesDraft = Current?.Notes;
        }
    }

    public async Task FocusNotesAsync()
    {
        if (notesArea is not null) await notesArea.SelectAsync();
    }

    private void OnNotesTyped(string value) => notesDraft = value;

    private Task OnNotesChange(IgbComponentValueChangedEventArgs _) => NotesCommitted.InvokeAsync(notesDraft);

    private Task OnStartChanged(DateTime? value)
    {
        if (Current is null || value is not { } start || start.Date == Current.Start.Date) return Task.CompletedTask;
        var finish = start.Date.AddDays(Current.IsMilestone ? 0 : Current.DurationDays);
        return DatesChanged.InvokeAsync(new DateEdit(start.Date, finish));
    }

    private Task OnFinishChanged(DateTime? value)
    {
        if (Current is null || value is not { } finish || finish.Date == Current.Finish.Date || finish.Date < Current.Start.Date)
            return Task.CompletedTask;
        return DatesChanged.InvokeAsync(new DateEdit(Current.Start, finish.Date));
    }
}
