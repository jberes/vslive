namespace modern_windows_forms.Client.Models.Projects;

/// <summary>GET /api/projects/lookup item.</summary>
public sealed record ProjectLookup(int Id, string Name);

/// <summary>Project header as returned inside the gantt payload and by GET /api/projects.</summary>
public sealed record ProjectDto(
    int Id, string Name, string? Description, string Status, DateTime StartDate, DateTime FinishDate,
    int? ManagerId, string? ManagerName, decimal? Budget, int TaskCount, int CompletedTaskCount,
    int MilestoneCount, decimal PercentComplete);

/// <summary>GET /api/projects/{id}/gantt — everything the schedule screen needs. Every task mutation returns this too.</summary>
public sealed record GanttPayload(
    ProjectDto Project, IReadOnlyList<GanttTask> Tasks, IReadOnlyList<GanttDependency> Dependencies, IReadOnlyList<GanttResource> Resources);

public sealed record GanttTask(
    int Id, int RowNumber, string Wbs, int Level, int? ParentId, int SortOrder, string Name,
    DateTime Start, DateTime Finish, double DurationDays, int PercentComplete, bool IsMilestone, bool IsSummary,
    string? Notes, string? Resources, string? Predecessors, IReadOnlyList<GanttAssignment> Assignments);

public sealed record GanttAssignment(int ResourceId, string ResourceName, int Units);

public sealed record GanttDependency(int Id, int PredecessorId, int SuccessorId, string Type, string TypeCode, double LagDays);

public sealed record GanttResource(int Id, string Name, string? Role, decimal? HourlyRate);

/// <summary>POST /api/projects/{id}/tasks response.</summary>
public sealed record TaskCreated(int TaskId, GanttPayload Gantt);

/// <summary>Derived schedule status shown as a chip (Done / Behind / In progress / Not started).</summary>
public enum ScheduleStatus { NotStarted, InProgress, Behind, Done }

public static class ScheduleMath
{
    /// <summary>Percent a task should be at on <paramref name="today"/> if it progresses linearly.</summary>
    public static int? ExpectedPercent(GanttTask t, DateTime today) =>
        t.IsSummary || t.IsMilestone || t.DurationDays <= 0
            ? null
            : (int)Math.Round(Math.Clamp((today - t.Start).TotalDays / t.DurationDays * 100, 0, 100));

    public static ScheduleStatus StatusOf(GanttTask t, DateTime today)
    {
        if (t.PercentComplete >= 100) return ScheduleStatus.Done;
        var expected = ExpectedPercent(t, today);
        if (expected is { } e && t.PercentComplete < e && t.Start <= today) return ScheduleStatus.Behind;
        if (t.IsMilestone && t.Start < today.Date) return ScheduleStatus.Behind;
        return t.Start <= today ? ScheduleStatus.InProgress : ScheduleStatus.NotStarted;
    }

    /// <summary>A summary is behind when any leaf below it is behind.</summary>
    public static ScheduleStatus PhaseStatus(GanttTask phase, IEnumerable<GanttTask> leaves, DateTime today)
    {
        if (phase.PercentComplete >= 100) return ScheduleStatus.Done;
        var list = leaves.ToList();
        if (list.Any(l => StatusOf(l, today) == ScheduleStatus.Behind)) return ScheduleStatus.Behind;
        return phase.Start <= today ? ScheduleStatus.InProgress : ScheduleStatus.NotStarted;
    }

    public static string Label(ScheduleStatus s) => s switch
    {
        ScheduleStatus.Done => "Done",
        ScheduleStatus.Behind => "Behind",
        ScheduleStatus.InProgress => "In progress",
        _ => "Not started"
    };

    /// <summary>StatusChip tone. Behind uses the warning tone, as in the design.</summary>
    public static string Tone(ScheduleStatus s) => s switch
    {
        ScheduleStatus.Done => "success",
        ScheduleStatus.Behind => "warning",
        ScheduleStatus.InProgress => "info",
        _ => "neutral"
    };

    public static string ProjectTone(string status) => status switch
    {
        "Active" => "info",
        "Completed" => "success",
        "OnHold" => "warning",
        "Cancelled" => "error",
        _ => "neutral"
    };

    public static string ProjectStatusLabel(string status) => status == "OnHold" ? "On hold" : status;
}
