using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Projects;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class ResourceDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
    public double HourlyRate { get; init; }
    public int? EmployeeId { get; init; }
    public int AssignmentCount { get; init; }
    public int OpenAssignmentCount { get; init; }
    public string AvatarUrl => Avatars.Url("resources", Id);
}

public sealed class ResourceInput
{
    [Required, StringLength(128)] public string Name { get; set; } = "";
    [EmailAddress] public string? Email { get; set; }
    [StringLength(64)] public string? Role { get; set; }
    [Range(0, 10_000)] public decimal HourlyRate { get; set; }
    public int? EmployeeId { get; set; }
}

public static class ResourceEndpoints
{
    public static void MapResourceEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/resources").WithTags("Resources");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Resources).ToPagedResultAsync(q, s => r =>
                r.Name.ToLower().Contains(s) || (r.Role ?? "").ToLower().Contains(s) || (r.Email ?? "").ToLower().Contains(s), "name", ct));

        g.MapGet("/lookup", async (AppDbContext db, CancellationToken ct) =>
            await db.Resources.OrderBy(r => r.Name).Select(r => new { r.Id, r.Name, r.Role }).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Resources.Where(r => r.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        // Assignments plus a weekly allocation histogram (100 = one full-time week).
        g.MapGet("/{id:int}/workload", async (int id, DateTime? from, DateTime? to, AppDbContext db, CancellationToken ct) =>
        {
            var resource = await Project(db.Resources.Where(r => r.Id == id)).FirstOrDefaultAsync(ct);
            if (resource is null) return Results.NotFound();
            var start = StartOfWeek(from ?? DateTime.Today.AddDays(-28));
            var end = to ?? start.AddDays(7 * 16);

            var assignments = await db.TaskAssignments.Where(a => a.ResourceId == id && !a.Task!.IsSummary)
                .OrderBy(a => a.Task!.Start)
                .Select(a => new
                {
                    a.TaskId,
                    TaskName = a.Task!.Name,
                    a.Task.ProjectId,
                    ProjectName = a.Task.Project!.Name,
                    a.Task.Start,
                    a.Task.Finish,
                    a.Task.DurationDays,
                    a.Task.PercentComplete,
                    a.Units,
                }).ToListAsync(ct);

            var weeks = new List<object>();
            for (var w = start; w < end; w = w.AddDays(7))
            {
                var weekEnd = w.AddDays(7);
                var allocation = assignments.Sum(a =>
                {
                    var overlapStart = a.Start > w ? a.Start : w;
                    var overlapEnd = a.Finish < weekEnd ? a.Finish : weekEnd;
                    var days = Math.Max(0, (overlapEnd - overlapStart).TotalDays);
                    return days / 7 * a.Units;
                });
                weeks.Add(new { weekStart = w, allocationPercent = Math.Round(allocation, 1), overAllocated = allocation > 100.5 });
            }

            return Results.Ok(new { resource, assignments, weeks });
        });

        g.MapPost("/", async (ResourceInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = new Resource { Name = input.Name };
            Apply(entity, input);
            db.Resources.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/resources/{entity.Id}", await Project(db.Resources.Where(r => r.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, ResourceInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Resources.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            Apply(entity, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Resources.Where(r => r.Id == id)).FirstAsync(ct));
        });

        // Removes the resource and its task assignments; projects it manages lose their manager.
        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Resources.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            db.Resources.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    // Unspecified kind keeps the JSON format identical to dates read from the database (no UTC offset).
    private static DateTime StartOfWeek(DateTime d) =>
        DateTime.SpecifyKind(d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)), DateTimeKind.Unspecified);

    private static IQueryable<ResourceDto> Project(IQueryable<Resource> q) => q.Select(r => new ResourceDto
    {
        Id = r.Id,
        Name = r.Name,
        Email = r.Email,
        Role = r.Role,
        HourlyRate = (double)r.HourlyRate,
        EmployeeId = r.EmployeeId,
        AssignmentCount = r.Assignments.Count,
        OpenAssignmentCount = r.Assignments.Count(a => a.Task!.PercentComplete < 100),
    });

    private static void Apply(Resource r, ResourceInput i)
    {
        r.Name = i.Name.Trim();
        r.Email = i.Email;
        r.Role = i.Role;
        r.HourlyRate = i.HourlyRate;
        r.EmployeeId = i.EmployeeId;
    }
}
