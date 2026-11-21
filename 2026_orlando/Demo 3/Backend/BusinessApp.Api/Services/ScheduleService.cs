using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Projects;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Services;

/// <summary>
/// Lightweight project scheduler:
/// <list type="number">
/// <item>Pushes successor tasks forward so every dependency (FS/SS/FF/SF + lag) is satisfied.</item>
/// <item>Rolls summary tasks up from their children (start, finish, duration, weighted % complete).</item>
/// </list>
/// Durations are calendar days.
/// </summary>
public sealed class ScheduleService(AppDbContext db)
{
    public async Task RecalculateAsync(int projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return;

        var tasks = await db.ProjectTasks.Where(t => t.ProjectId == projectId).ToListAsync(ct);
        var taskIds = tasks.Select(t => t.Id).ToHashSet();
        var deps = await db.TaskDependencies.Where(d => taskIds.Contains(d.SuccessorId)).ToListAsync(ct);

        Recalculate(project, tasks, deps);
        await db.SaveChangesAsync(ct);
    }

    public static void Recalculate(Project project, List<ProjectTask> tasks, List<TaskDependency> deps)
    {
        var byId = tasks.ToDictionary(t => t.Id);
        var childrenOf = tasks.Where(t => t.ParentId is not null).ToLookup(t => t.ParentId!.Value);

        foreach (var t in tasks)
        {
            t.IsSummary = childrenOf[t.Id].Any();
            if (t.IsSummary) continue;
            if (t.IsMilestone) t.DurationDays = 0;
            t.DurationDays = Math.Max(0, t.DurationDays);
            t.Finish = t.Start.AddDays(t.DurationDays);
        }

        // Iterative relaxation is simple and handles dependencies on summary tasks too.
        for (var pass = 0; pass < tasks.Count + 1; pass++)
        {
            RollUp(tasks, childrenOf);
            var changed = false;
            foreach (var d in deps)
            {
                if (!byId.TryGetValue(d.PredecessorId, out var pred) || !byId.TryGetValue(d.SuccessorId, out var succ) || succ.IsSummary)
                    continue;

                var lag = d.LagDays;
                var earliestStart = d.Type switch
                {
                    DependencyType.FinishToStart => pred.Finish.AddDays(lag),
                    DependencyType.StartToStart => pred.Start.AddDays(lag),
                    DependencyType.FinishToFinish => pred.Finish.AddDays(lag - succ.DurationDays),
                    DependencyType.StartToFinish => pred.Start.AddDays(lag - succ.DurationDays),
                    _ => succ.Start,
                };
                if (succ.Start < earliestStart)
                {
                    succ.Start = earliestStart;
                    succ.Finish = earliestStart.AddDays(succ.DurationDays);
                    changed = true;
                }
            }
            if (!changed) break;
        }

        if (tasks.Count > 0)
        {
            project.StartDate = tasks.Min(t => t.Start);
            project.FinishDate = tasks.Max(t => t.Finish);
        }
        project.UpdatedAt = DateTime.UtcNow;
    }

    private static void RollUp(List<ProjectTask> tasks, ILookup<int, ProjectTask> childrenOf)
    {
        var roots = tasks.Where(t => t.ParentId is null);
        foreach (var root in roots) RollUp(root, childrenOf);
    }

    private static void RollUp(ProjectTask task, ILookup<int, ProjectTask> childrenOf)
    {
        var children = childrenOf[task.Id].ToList();
        if (children.Count == 0) return;
        foreach (var c in children) RollUp(c, childrenOf);

        task.Start = children.Min(c => c.Start);
        task.Finish = children.Max(c => c.Finish);
        task.DurationDays = Math.Round((task.Finish - task.Start).TotalDays, 2);
        task.IsMilestone = false;

        var weight = children.Sum(c => c.DurationDays);
        task.PercentComplete = weight > 0
            ? (int)Math.Round(children.Sum(c => c.PercentComplete * c.DurationDays) / weight)
            : (int)Math.Round(children.Average(c => c.PercentComplete));
    }

    /// <summary>True when adding predecessor -> successor would create a cycle.</summary>
    public async Task<bool> WouldCreateCycleAsync(int predecessorId, int successorId, CancellationToken ct = default)
    {
        if (predecessorId == successorId) return true;
        var edges = await db.TaskDependencies.Select(d => new { d.PredecessorId, d.SuccessorId }).ToListAsync(ct);
        var next = edges.ToLookup(e => e.PredecessorId, e => e.SuccessorId);

        var stack = new Stack<int>([successorId]);
        var seen = new HashSet<int>();
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (id == predecessorId) return true;
            if (!seen.Add(id)) continue;
            foreach (var n in next[id]) stack.Push(n);
        }
        return false;
    }
}
