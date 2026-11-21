using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Projects;
using BusinessApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class TaskInput
{
    [Required, StringLength(256)] public string Name { get; set; } = "";
    /// <summary>Insert as a child of this task (ignored when AfterTaskId is set).</summary>
    public int? ParentId { get; set; }
    /// <summary>Insert directly below this task, as its sibling.</summary>
    public int? AfterTaskId { get; set; }
    public DateTime? Start { get; set; }
    [Range(0, 3650)] public double? DurationDays { get; set; }
    [Range(0, 100)] public int PercentComplete { get; set; }
    public bool IsMilestone { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
    public List<AssignmentInput>? Assignments { get; set; }
}

public sealed class TaskUpdateInput
{
    [Required, StringLength(256)] public string Name { get; set; } = "";
    public DateTime Start { get; set; }
    [Range(0, 3650)] public double DurationDays { get; set; }
    [Range(0, 100)] public int PercentComplete { get; set; }
    public bool IsMilestone { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
}

public sealed class AssignmentInput
{
    public int ResourceId { get; set; }
    [Range(1, 1000)] public int Units { get; set; } = 100;
}

public sealed class ProgressInput { [Range(0, 100)] public int PercentComplete { get; set; } }
public sealed class NotesInput { [StringLength(4000)] public string? Notes { get; set; } }
public sealed class MoveInput { [Required, RegularExpression("^(up|down)$")] public string Direction { get; set; } = "up"; }

public sealed class FormatInput
{
    [StringLength(64)] public string? FontFamily { get; set; }
    [Range(6, 72)] public double? FontSize { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    [RegularExpression("^#[0-9a-fA-F]{6}$")] public string? ForegroundColor { get; set; }
    [RegularExpression("^#[0-9a-fA-F]{6}$")] public string? BackgroundColor { get; set; }
}

/// <summary>
/// Task-level operations backing the Gantt ribbon (Insert Task/Milestone, Indent/Outdent, Move,
/// % Complete, Notes, Font, Assign Resources). Every mutation re-runs the scheduler and returns the refreshed Gantt.
/// </summary>
public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/projects/{projectId:int}/tasks", async (int projectId, TaskInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
        {
            var project = await db.Projects.FindAsync([projectId], ct);
            if (project is null) return Results.NotFound();

            int? parentId = input.ParentId;
            int sortOrder;
            DateTime start;
            if (input.AfterTaskId is int afterId)
            {
                var after = await db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == afterId && t.ProjectId == projectId, ct);
                if (after is null) return Invalid("afterTaskId", "Task not found in this project.");
                parentId = after.ParentId;
                sortOrder = after.SortOrder + 1;
                start = input.Start ?? after.Finish;
                await db.ProjectTasks.Where(t => t.ProjectId == projectId && t.ParentId == parentId && t.SortOrder >= sortOrder)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.SortOrder, t => t.SortOrder + 1), ct);
            }
            else
            {
                if (parentId is int p && !await db.ProjectTasks.AnyAsync(t => t.Id == p && t.ProjectId == projectId, ct))
                    return Invalid("parentId", "Parent task not found in this project.");
                sortOrder = await db.ProjectTasks.Where(t => t.ProjectId == projectId && t.ParentId == parentId).MaxAsync(t => (int?)t.SortOrder, ct) + 1 ?? 0;
                start = input.Start ?? (parentId is int pp ? (await db.ProjectTasks.FindAsync([pp], ct))!.Start : project.StartDate);
            }

            if (input.Assignments is { Count: > 0 } && await InvalidResources(db, input.Assignments, ct) is { } bad) return bad;

            var duration = input.IsMilestone ? 0 : input.DurationDays ?? 1;
            var task = new ProjectTask
            {
                ProjectId = projectId,
                ParentId = parentId,
                SortOrder = sortOrder,
                Name = input.Name.Trim(),
                Start = start,
                DurationDays = duration,
                Finish = start.AddDays(duration),
                PercentComplete = input.PercentComplete,
                IsMilestone = input.IsMilestone,
                Notes = input.Notes,
                Assignments = input.Assignments?.Select(a => new TaskAssignment { ResourceId = a.ResourceId, Units = a.Units }).ToList() ?? [],
            };
            db.ProjectTasks.Add(task);
            await db.SaveChangesAsync(ct);
            await schedule.RecalculateAsync(projectId, ct);
            return Results.Created($"/api/tasks/{task.Id}", new { taskId = task.Id, gantt = await ProjectEndpoints.BuildGanttAsync(db, projectId, ct) });
        }).WithTags("Tasks");

        var g = api.MapGroup("/tasks").WithTags("Tasks");

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.ProjectTasks.AsNoTracking()
                .Include(x => x.Assignments).ThenInclude(a => a.Resource)
                .Include(x => x.Predecessors)
                .Include(x => x.Successors)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (t is null) return Results.NotFound();
            return Results.Ok(new
            {
                t.Id, t.ProjectId, t.ParentId, t.SortOrder, t.Name, t.Start, t.Finish, t.DurationDays, t.PercentComplete,
                t.IsMilestone, t.IsSummary, t.Notes,
                format = new TaskFormat(t.FontFamily, t.FontSize, t.IsBold, t.IsItalic, t.IsUnderline, t.ForegroundColor, t.BackgroundColor),
                assignments = t.Assignments.Select(a => new AssignmentDto(a.ResourceId, a.Resource!.Name, a.Units)),
                predecessors = t.Predecessors.Select(ProjectEndpoints.ToDto),
                successors = t.Successors.Select(ProjectEndpoints.ToDto),
            });
        });

        g.MapPut("/{id:int}", (int id, TaskUpdateInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, (t, _) =>
            {
                t.Name = input.Name.Trim();
                t.Notes = input.Notes;
                if (!t.IsSummary)
                {
                    // Summary tasks are rolled up from their children, so dates/progress are ignored for them.
                    t.Start = input.Start;
                    t.IsMilestone = input.IsMilestone;
                    t.DurationDays = input.IsMilestone ? 0 : input.DurationDays;
                    t.PercentComplete = input.PercentComplete;
                }
                return Task.FromResult<IResult?>(null);
            }));

        g.MapPatch("/{id:int}/progress", (int id, ProgressInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, async (t, _) =>
            {
                if (t.IsSummary)
                {
                    // Like the ribbon's 0%..100% buttons applied to a summary: apply to every leaf below it.
                    var all = await db.ProjectTasks.Where(x => x.ProjectId == t.ProjectId).ToListAsync(ct);
                    foreach (var leaf in Descendants(all, t.Id).Where(x => !x.IsSummary)) leaf.PercentComplete = input.PercentComplete;
                }
                else t.PercentComplete = input.PercentComplete;
                return null;
            }));

        g.MapPut("/{id:int}/notes", (int id, NotesInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, (t, _) => { t.Notes = input.Notes; return Task.FromResult<IResult?>(null); }));

        g.MapPut("/{id:int}/format", (int id, FormatInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, (t, _) =>
            {
                t.FontFamily = input.FontFamily;
                t.FontSize = input.FontSize;
                t.IsBold = input.IsBold;
                t.IsItalic = input.IsItalic;
                t.IsUnderline = input.IsUnderline;
                t.ForegroundColor = input.ForegroundColor;
                t.BackgroundColor = input.BackgroundColor;
                return Task.FromResult<IResult?>(null);
            }));

        g.MapPut("/{id:int}/assignments", (int id, List<AssignmentInput> input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, async (t, _) =>
            {
                if (input.Select(a => a.ResourceId).Distinct().Count() != input.Count) return Invalid("resourceId", "Duplicate resources.");
                if (await InvalidResources(db, input, ct) is { } bad) return bad;
                await db.TaskAssignments.Where(a => a.TaskId == id).ExecuteDeleteAsync(ct);
                db.TaskAssignments.AddRange(input.Select(a => new TaskAssignment { TaskId = id, ResourceId = a.ResourceId, Units = a.Units }));
                return null;
            }));

        // Make the task a child of the sibling directly above it.
        g.MapPost("/{id:int}/indent", (int id, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, async (t, siblings) =>
            {
                var above = siblings.Where(s => s.SortOrder < t.SortOrder).MaxBy(s => s.SortOrder);
                if (above is null) return Results.Problem("The first task in a group cannot be indented.", statusCode: 409);
                var newSortOrder = await db.ProjectTasks.Where(x => x.ParentId == above.Id).MaxAsync(x => (int?)x.SortOrder, ct) + 1 ?? 0;
                t.ParentId = above.Id;
                t.SortOrder = newSortOrder;
                Renumber(siblings.Where(s => s.Id != t.Id).OrderBy(s => s.SortOrder));
                return null;
            }));

        // Promote the task to its parent's level, directly below the old parent.
        g.MapPost("/{id:int}/outdent", (int id, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, async (t, siblings) =>
            {
                if (t.ParentId is not int parentId) return Results.Problem("Top-level tasks cannot be outdented.", statusCode: 409);
                var parent = await db.ProjectTasks.FirstAsync(x => x.Id == parentId, ct);
                var newSiblings = await db.ProjectTasks.Where(x => x.ProjectId == t.ProjectId && x.ParentId == parent.ParentId)
                    .OrderBy(x => x.SortOrder).ToListAsync(ct);
                t.ParentId = parent.ParentId;
                Renumber(siblings.Where(s => s.Id != t.Id).OrderBy(s => s.SortOrder));
                newSiblings.Insert(newSiblings.IndexOf(parent) + 1, t);
                Renumber(newSiblings);
                return null;
            }));

        g.MapPost("/{id:int}/move", (int id, MoveInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
            Mutate(id, db, schedule, ct, (t, siblings) =>
            {
                var up = input.Direction == "up";
                var other = up
                    ? siblings.Where(s => s.SortOrder < t.SortOrder).MaxBy(s => s.SortOrder)
                    : siblings.Where(s => s.SortOrder > t.SortOrder).MinBy(s => s.SortOrder);
                if (other is null) return Task.FromResult<IResult?>(Results.Problem($"The task cannot be moved {input.Direction}.", statusCode: 409));
                (t.SortOrder, other.SortOrder) = (other.SortOrder, t.SortOrder);
                return Task.FromResult<IResult?>(null);
            }));

        // Deletes the task and its entire subtree.
        g.MapDelete("/{id:int}", async (int id, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
        {
            var task = await db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (task is null) return Results.NotFound();
            var all = await db.ProjectTasks.Where(t => t.ProjectId == task.ProjectId).ToListAsync(ct);
            var ids = Descendants(all, id).Select(t => t.Id).Append(id).ToList();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.ProjectTasks.Where(t => ids.Contains(t.Id)).ExecuteUpdateAsync(s => s.SetProperty(t => t.ParentId, (int?)null), ct);
            await db.ProjectTasks.Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);

            db.ChangeTracker.Clear();
            var siblings = await db.ProjectTasks.Where(t => t.ProjectId == task.ProjectId && t.ParentId == task.ParentId).ToListAsync(ct);
            Renumber(siblings.OrderBy(s => s.SortOrder));
            await db.SaveChangesAsync(ct);
            await schedule.RecalculateAsync(task.ProjectId, ct);
            return Results.Ok(new { deleted = ids.Count, gantt = await ProjectEndpoints.BuildGanttAsync(db, task.ProjectId, ct) });
        });
    }

    /// <summary>Loads the task and its siblings, applies a change, reschedules and returns the refreshed Gantt.</summary>
    private static async Task<IResult> Mutate(int id, AppDbContext db, ScheduleService schedule, CancellationToken ct,
        Func<ProjectTask, List<ProjectTask>, Task<IResult?>> change)
    {
        var task = await db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return Results.NotFound();
        var siblings = await db.ProjectTasks.Where(t => t.ProjectId == task.ProjectId && t.ParentId == task.ParentId).ToListAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await change(task, siblings) is { } error) return error;
        await db.SaveChangesAsync(ct);
        await schedule.RecalculateAsync(task.ProjectId, ct);
        await tx.CommitAsync(ct);
        return Results.Ok(await ProjectEndpoints.BuildGanttAsync(db, task.ProjectId, ct));
    }

    private static IEnumerable<ProjectTask> Descendants(List<ProjectTask> all, int id)
    {
        var children = all.ToLookup(t => t.ParentId);
        var stack = new Stack<int>([id]);
        while (stack.Count > 0)
            foreach (var c in children[stack.Pop()])
            {
                yield return c;
                stack.Push(c.Id);
            }
    }

    private static void Renumber(IEnumerable<ProjectTask> ordered)
    {
        var i = 0;
        foreach (var t in ordered.ToList()) t.SortOrder = i++;
    }

    private static async Task<IResult?> InvalidResources(AppDbContext db, List<AssignmentInput> input, CancellationToken ct)
    {
        var ids = input.Select(a => a.ResourceId).Distinct().ToList();
        return await db.Resources.CountAsync(r => ids.Contains(r.Id), ct) == ids.Count ? null : Invalid("resourceId", "One or more resources were not found.");
    }

    private static IResult Invalid(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
