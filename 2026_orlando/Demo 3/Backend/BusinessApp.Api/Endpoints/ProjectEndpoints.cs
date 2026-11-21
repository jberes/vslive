using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Projects;
using BusinessApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class ProjectDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public ProjectStatus Status { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? FinishDate { get; init; }
    public int? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public double Budget { get; init; }
    public int TaskCount { get; init; }
    public int CompletedTaskCount { get; init; }
    public int MilestoneCount { get; init; }
    public double PercentComplete { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ProjectInput
{
    [Required, StringLength(128)] public string Name { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public DateTime StartDate { get; set; }
    public int? ManagerId { get; set; }
    public int? CustomerId { get; set; }
    [Range(0, 100_000_000)] public decimal Budget { get; set; }
}

public sealed class DependencyInput
{
    public int PredecessorId { get; set; }
    public int SuccessorId { get; set; }
    public DependencyType Type { get; set; } = DependencyType.FinishToStart;
    [Range(-365, 365)] public double LagDays { get; set; }
}

/// <summary>One row of the Gantt grid, in display order.</summary>
public sealed record GanttRow(
    int Id, int RowNumber, string Wbs, int Level, int? ParentId, int SortOrder, string Name,
    DateTime Start, DateTime Finish, double DurationDays, int PercentComplete, bool IsMilestone, bool IsSummary,
    string? Notes, string? Resources, string? Predecessors, TaskFormat Format, IReadOnlyList<AssignmentDto> Assignments);

public sealed record TaskFormat(string? FontFamily, double? FontSize, bool IsBold, bool IsItalic, bool IsUnderline, string? ForegroundColor, string? BackgroundColor);

public sealed record AssignmentDto(int ResourceId, string ResourceName, int Units);

public sealed record DependencyDto(int Id, int PredecessorId, int SuccessorId, DependencyType Type, string TypeCode, double LagDays);

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/projects").WithTags("Projects");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Projects).ToPagedResultAsync(q, s => p =>
                p.Name.ToLower().Contains(s) || (p.ManagerName ?? "").ToLower().Contains(s) || (p.CustomerName ?? "").ToLower().Contains(s),
                "startDate", ct));

        g.MapGet("/lookup", async (AppDbContext db, CancellationToken ct) =>
            await db.Projects.OrderBy(p => p.Name).Select(p => new LookupItem(p.Id, p.Name)).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Projects.Where(p => p.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/gantt", async (int id, AppDbContext db, CancellationToken ct) =>
            await BuildGanttAsync(db, id, ct) is { } gantt ? Results.Ok(gantt) : Results.NotFound());

        g.MapGet("/{id:int}/dashboard", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var project = await Project(db.Projects.Where(p => p.Id == id)).FirstOrDefaultAsync(ct);
            if (project is null) return Results.NotFound();
            var today = DateTime.Today;
            var leaves = db.ProjectTasks.Where(t => t.ProjectId == id && !t.IsSummary);
            var workload = await db.TaskAssignments.Where(a => a.Task!.ProjectId == id && !a.Task.IsSummary)
                .GroupBy(a => a.Resource!.Name)
                .Select(g => new { g.Key, Days = g.Sum(a => a.Task!.DurationDays * a.Units / 100.0) })
                .OrderByDescending(x => x.Days).ToListAsync(ct);
            var cost = await db.TaskAssignments.Where(a => a.Task!.ProjectId == id && !a.Task.IsSummary)
                .SumAsync(a => a.Task!.DurationDays * 8 * a.Units / 100.0 * (double)a.Resource!.HourlyRate, ct);

            return Results.Ok(new
            {
                project,
                estimatedCost = Money.Round(cost),
                budgetVariance = Money.Round(project.Budget - cost),
                taskStatus = new[]
                {
                    new NameValue("Completed", await leaves.CountAsync(t => t.PercentComplete == 100, ct)),
                    new NameValue("In Progress", await leaves.CountAsync(t => t.PercentComplete > 0 && t.PercentComplete < 100, ct)),
                    new NameValue("Not Started", await leaves.CountAsync(t => t.PercentComplete == 0 && t.Start >= today, ct)),
                    new NameValue("Late", await leaves.CountAsync(t => t.PercentComplete < 100 && t.Finish < today, ct)),
                },
                workloadDays = workload.Select(x => new NameValue(x.Key, Math.Round(x.Days, 1))),
                upcomingMilestones = await db.ProjectTasks.Where(t => t.ProjectId == id && t.IsMilestone && t.PercentComplete < 100)
                    .OrderBy(t => t.Start).Select(t => new { t.Id, t.Name, Date = t.Start }).Take(10).ToListAsync(ct),
            });
        });

        g.MapPost("/", async (ProjectInput input, AppDbContext db, CancellationToken ct) =>
        {
            if (await ValidateRefs(db, input, ct) is { } problem) return problem;
            var now = DateTime.UtcNow;
            var entity = new Project { Name = input.Name, CreatedAt = now };
            Apply(entity, input);
            entity.FinishDate = entity.StartDate;
            entity.UpdatedAt = now;
            db.Projects.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/projects/{entity.Id}", await Project(db.Projects.Where(p => p.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, ProjectInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
        {
            var entity = await db.Projects.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            if (await ValidateRefs(db, input, ct) is { } problem) return problem;

            // Moving the project start shifts every task by the same amount.
            var shift = input.StartDate.Date - entity.StartDate.Date;
            Apply(entity, input);
            if (shift != TimeSpan.Zero)
            {
                var tasks = await db.ProjectTasks.Where(t => t.ProjectId == id).ToListAsync(ct);
                foreach (var t in tasks) { t.Start += shift; t.Finish += shift; }
            }
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await schedule.RecalculateAsync(id, ct);
            return Results.Ok(await Project(db.Projects.Where(p => p.Id == id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.Projects.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Parent links are RESTRICT, so flatten the tree before the cascade delete.
            await db.ProjectTasks.Where(t => t.ProjectId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.ParentId, (int?)null), ct);
            await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:int}/recalculate", async (int id, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
        {
            if (!await db.Projects.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();
            await schedule.RecalculateAsync(id, ct);
            return Results.Ok(await BuildGanttAsync(db, id, ct));
        });

        // Dependencies (Gantt links).
        g.MapGet("/{id:int}/dependencies", async (int id, AppDbContext db, CancellationToken ct) =>
            (await db.TaskDependencies.Where(d => d.Successor!.ProjectId == id).ToListAsync(ct)).Select(ToDto));

        g.MapPost("/{id:int}/dependencies", async (int id, DependencyInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
        {
            var ids = new[] { input.PredecessorId, input.SuccessorId };
            var count = await db.ProjectTasks.CountAsync(t => t.ProjectId == id && ids.Contains(t.Id), ct);
            if (count != 2 || input.PredecessorId == input.SuccessorId)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["predecessorId"] = ["Both tasks must exist in this project and be different."] });
            if (await db.TaskDependencies.AnyAsync(d => d.PredecessorId == input.PredecessorId && d.SuccessorId == input.SuccessorId, ct))
                return Results.Problem("That dependency already exists.", statusCode: 409);
            if (await schedule.WouldCreateCycleAsync(input.PredecessorId, input.SuccessorId, ct))
                return Results.Problem("That dependency would create a circular reference.", statusCode: 409);

            var dep = new TaskDependency { PredecessorId = input.PredecessorId, SuccessorId = input.SuccessorId, Type = input.Type, LagDays = input.LagDays };
            db.TaskDependencies.Add(dep);
            await db.SaveChangesAsync(ct);
            await schedule.RecalculateAsync(id, ct);
            return Results.Created($"/api/dependencies/{dep.Id}", ToDto(dep));
        });

        var deps = api.MapGroup("/dependencies").WithTags("Projects");

        deps.MapPut("/{id:int}", async (int id, DependencyInput input, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
        {
            var dep = await db.TaskDependencies.Include(d => d.Successor).FirstOrDefaultAsync(d => d.Id == id, ct);
            if (dep is null) return Results.NotFound();
            dep.Type = input.Type;
            dep.LagDays = input.LagDays;
            await db.SaveChangesAsync(ct);
            await schedule.RecalculateAsync(dep.Successor!.ProjectId, ct);
            return Results.Ok(ToDto(dep));
        });

        deps.MapDelete("/{id:int}", async (int id, AppDbContext db, ScheduleService schedule, CancellationToken ct) =>
        {
            var projectId = await db.TaskDependencies.Where(d => d.Id == id).Select(d => (int?)d.Successor!.ProjectId).FirstOrDefaultAsync(ct);
            if (projectId is null) return Results.NotFound();
            await db.TaskDependencies.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
            await schedule.RecalculateAsync(projectId.Value, ct);
            return Results.NoContent();
        });
    }

    internal static async Task<object?> BuildGanttAsync(AppDbContext db, int projectId, CancellationToken ct)
    {
        var project = await Project(db.Projects.Where(p => p.Id == projectId)).FirstOrDefaultAsync(ct);
        if (project is null) return null;

        var tasks = await db.ProjectTasks.AsNoTracking().Where(t => t.ProjectId == projectId).ToListAsync(ct);
        var taskIds = tasks.Select(t => t.Id).ToList();
        var assignments = await db.TaskAssignments.Where(a => taskIds.Contains(a.TaskId))
            .Select(a => new { a.TaskId, a.ResourceId, a.Resource!.Name, a.Units }).ToListAsync(ct);
        var dependencies = await db.TaskDependencies.AsNoTracking().Where(d => taskIds.Contains(d.SuccessorId)).ToListAsync(ct);
        var resources = await db.Resources.OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.Role, HourlyRate = (double)r.HourlyRate }).ToListAsync(ct);

        var ordered = Flatten(tasks);
        var rowOf = ordered.Select((x, i) => (x.Task.Id, Row: i + 1)).ToDictionary(x => x.Id, x => x.Row);
        var assignmentsOf = assignments.ToLookup(a => a.TaskId);
        var predecessorsOf = dependencies.ToLookup(d => d.SuccessorId);

        var rows = ordered.Select((x, i) =>
        {
            var t = x.Task;
            var taskAssignments = assignmentsOf[t.Id].OrderBy(a => a.Name).Select(a => new AssignmentDto(a.ResourceId, a.Name, a.Units)).ToList();
            var predecessors = string.Join(",", predecessorsOf[t.Id]
                .Where(d => rowOf.ContainsKey(d.PredecessorId))
                .OrderBy(d => rowOf[d.PredecessorId])
                .Select(d => $"{rowOf[d.PredecessorId]}{(d.Type == DependencyType.FinishToStart && d.LagDays == 0 ? "" : TypeCode(d.Type))}{Lag(d.LagDays)}"));
            return new GanttRow(t.Id, i + 1, x.Wbs, x.Level, t.ParentId, t.SortOrder, t.Name, t.Start, t.Finish, t.DurationDays,
                t.PercentComplete, t.IsMilestone, t.IsSummary, t.Notes,
                taskAssignments.Count == 0 ? null : string.Join(", ", taskAssignments.Select(a => a.Units == 100 ? a.ResourceName : $"{a.ResourceName}[{a.Units}%]")),
                predecessors.Length == 0 ? null : predecessors,
                new TaskFormat(t.FontFamily, t.FontSize, t.IsBold, t.IsItalic, t.IsUnderline, t.ForegroundColor, t.BackgroundColor),
                taskAssignments);
        }).ToList();

        return new { project, tasks = rows, dependencies = dependencies.Select(ToDto), resources };
    }

    /// <summary>Depth-first display order with WBS codes (1, 1.1, 1.2, 2 ...).</summary>
    internal static List<(ProjectTask Task, string Wbs, int Level)> Flatten(List<ProjectTask> tasks)
    {
        var children = tasks.ToLookup(t => t.ParentId);
        var result = new List<(ProjectTask, string, int)>(tasks.Count);
        void Walk(int? parentId, string prefix, int level)
        {
            var i = 0;
            foreach (var t in children[parentId].OrderBy(t => t.SortOrder).ThenBy(t => t.Id))
            {
                var wbs = prefix.Length == 0 ? $"{++i}" : $"{prefix}.{++i}";
                result.Add((t, wbs, level));
                Walk(t.Id, wbs, level + 1);
            }
        }
        Walk(null, "", 0);
        return result;
    }

    internal static IQueryable<ProjectDto> Project(IQueryable<Project> q) => q.Select(p => new ProjectDto
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Status = p.Status,
        StartDate = p.StartDate,
        FinishDate = p.FinishDate,
        ManagerId = p.ManagerId,
        ManagerName = p.Manager == null ? null : p.Manager.Name,
        CustomerId = p.CustomerId,
        CustomerName = p.Customer == null ? null : p.Customer.CompanyName,
        Budget = (double)p.Budget,
        TaskCount = p.Tasks.Count(t => !t.IsSummary),
        CompletedTaskCount = p.Tasks.Count(t => !t.IsSummary && t.PercentComplete == 100),
        MilestoneCount = p.Tasks.Count(t => t.IsMilestone),
        PercentComplete = Math.Round(p.Tasks.Where(t => !t.IsSummary).Sum(t => t.DurationDays) > 0
            ? p.Tasks.Where(t => !t.IsSummary).Sum(t => t.PercentComplete * t.DurationDays) / p.Tasks.Where(t => !t.IsSummary).Sum(t => t.DurationDays)
            : 0, 1),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    });

    internal static DependencyDto ToDto(TaskDependency d) => new(d.Id, d.PredecessorId, d.SuccessorId, d.Type, TypeCode(d.Type), d.LagDays);

    private static string TypeCode(DependencyType t) => t switch
    {
        DependencyType.StartToStart => "SS",
        DependencyType.FinishToFinish => "FF",
        DependencyType.StartToFinish => "SF",
        _ => "FS",
    };

    private static string Lag(double days) => days == 0 ? "" : days > 0 ? $"+{days:0.##} days" : $"{days:0.##} days";

    private static async Task<IResult?> ValidateRefs(AppDbContext db, ProjectInput input, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.ManagerId is int m && !await db.Resources.AnyAsync(r => r.Id == m, ct)) errors["managerId"] = ["Manager (resource) not found."];
        if (input.CustomerId is int c && !await db.Customers.AnyAsync(x => x.Id == c, ct)) errors["customerId"] = ["Customer not found."];
        if (input.StartDate == default) errors["startDate"] = ["Start date is required."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static void Apply(Project p, ProjectInput i)
    {
        p.Name = i.Name.Trim();
        p.Description = i.Description;
        p.Status = i.Status;
        p.StartDate = i.StartDate.Date;
        p.ManagerId = i.ManagerId;
        p.CustomerId = i.CustomerId;
        p.Budget = i.Budget;
    }
}
