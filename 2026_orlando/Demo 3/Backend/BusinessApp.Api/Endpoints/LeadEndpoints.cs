using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Marketing;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class LeadDto
{
    public int Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string FullName => $"{FirstName} {LastName}";
    public string? Title { get; init; }
    public required string Company { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public LeadSource Source { get; init; }
    public LeadStatus Status { get; init; }
    public int Score { get; init; }
    public double EstimatedValue { get; init; }
    public int? CampaignId { get; init; }
    public string? CampaignName { get; init; }
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public int? ConvertedCustomerId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ConvertedAt { get; init; }
    public DateTime? LastActivityAt { get; init; }
    public int ActivityCount { get; init; }
    public string AvatarUrl => Avatars.Url("leads", Id);
}

public sealed class LeadInput
{
    [Required, StringLength(64)] public string FirstName { get; set; } = "";
    [Required, StringLength(64)] public string LastName { get; set; } = "";
    [StringLength(64)] public string? Title { get; set; }
    [Required, StringLength(128)] public string Company { get; set; } = "";
    [EmailAddress] public string? Email { get; set; }
    [StringLength(32)] public string? Phone { get; set; }
    [StringLength(64)] public string? City { get; set; }
    [StringLength(64)] public string? Country { get; set; }
    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    [Range(0, 100)] public int Score { get; set; }
    [Range(0, 100_000_000)] public decimal EstimatedValue { get; set; }
    public int? CampaignId { get; set; }
    public int? OwnerId { get; set; }
}

public sealed class LeadStatusInput { public LeadStatus Status { get; set; } }

public sealed class ActivityInput
{
    public ActivityType Type { get; set; }
    [Required, StringLength(256)] public string Subject { get; set; } = "";
    [StringLength(4000)] public string? Notes { get; set; }
    public DateTime? OccurredAt { get; set; }
    public int? EmployeeId { get; set; }
}

public static class LeadEndpoints
{
    public static void MapLeadEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/leads").WithTags("Marketing");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Leads).ToPagedResultAsync(q, Search, "-createdAt", ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Leads.Where(l => l.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapPost("/", async (LeadInput input, AppDbContext db, CancellationToken ct) =>
        {
            if (await ValidateRefs(db, input.CampaignId, input.OwnerId, ct) is { } problem) return problem;
            var entity = new Lead { FirstName = input.FirstName, LastName = input.LastName, Company = input.Company, CreatedAt = DateTime.Now };
            Apply(entity, input);
            db.Leads.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/leads/{entity.Id}", await Project(db.Leads.Where(l => l.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, LeadInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Leads.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            if (await ValidateRefs(db, input.CampaignId, input.OwnerId, ct) is { } problem) return problem;
            Apply(entity, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Leads.Where(l => l.Id == id)).FirstAsync(ct));
        });

        g.MapPatch("/{id:int}/status", async (int id, LeadStatusInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Leads.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            entity.Status = input.Status;
            if (input.Status != LeadStatus.Won) entity.ConvertedAt = null;
            else entity.ConvertedAt ??= DateTime.Now;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Leads.Where(l => l.Id == id)).FirstAsync(ct));
        });

        // Converts a lead into a Sales customer and marks it Won.
        g.MapPost("/{id:int}/convert", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var lead = await db.Leads.FindAsync([id], ct);
            if (lead is null) return Results.NotFound();
            if (lead.ConvertedCustomerId is not null)
                return Results.Problem("Lead has already been converted.", statusCode: 409);

            var customer = new Customer
            {
                Code = await CustomerEndpoints.GenerateCode(lead.Company, db, ct),
                CompanyName = lead.Company,
                ContactName = $"{lead.FirstName} {lead.LastName}",
                ContactTitle = lead.Title,
                Email = lead.Email,
                Phone = lead.Phone,
                City = lead.City,
                Country = lead.Country,
                CreatedAt = DateTime.UtcNow,
            };
            lead.ConvertedCustomer = customer;
            lead.Status = LeadStatus.Won;
            lead.ConvertedAt = DateTime.Now;
            db.LeadActivities.Add(new LeadActivity { LeadId = id, Type = ActivityType.Note, Subject = $"Converted to customer {customer.Code}", OccurredAt = DateTime.Now, EmployeeId = lead.OwnerId });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                lead = await Project(db.Leads.Where(l => l.Id == id)).FirstAsync(ct),
                customer = await CustomerEndpoints.Project(db.Customers.Where(c => c.Id == customer.Id)).FirstAsync(ct),
            });
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Leads.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            db.Leads.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/{id:int}/activities", async (int id, AppDbContext db, CancellationToken ct) =>
            await db.Leads.AnyAsync(l => l.Id == id, ct)
                ? Results.Ok(await ProjectActivities(db.LeadActivities.Where(a => a.LeadId == id).OrderByDescending(a => a.OccurredAt)).ToListAsync(ct))
                : Results.NotFound());

        g.MapPost("/{id:int}/activities", async (int id, ActivityInput input, AppDbContext db, CancellationToken ct) =>
        {
            var lead = await db.Leads.FindAsync([id], ct);
            if (lead is null) return Results.NotFound();
            if (input.EmployeeId is int e && !await db.Employees.AnyAsync(x => x.Id == e, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["employeeId"] = ["Employee not found."] });

            var activity = new LeadActivity
            {
                LeadId = id, Type = input.Type, Subject = input.Subject.Trim(), Notes = input.Notes,
                OccurredAt = input.OccurredAt ?? DateTime.Now, EmployeeId = input.EmployeeId ?? lead.OwnerId,
            };
            db.LeadActivities.Add(activity);
            if (lead.Status == LeadStatus.New) lead.Status = LeadStatus.Contacted;
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/leads/{id}/activities/{activity.Id}",
                await ProjectActivities(db.LeadActivities.Where(a => a.Id == activity.Id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}/activities/{activityId:int}", async (int id, int activityId, AppDbContext db, CancellationToken ct) =>
            await db.LeadActivities.Where(a => a.Id == activityId && a.LeadId == id).ExecuteDeleteAsync(ct) > 0 ? Results.NoContent() : Results.NotFound());
    }

    internal static Expression<Func<LeadDto, bool>> Search(string s) => l =>
        l.FirstName.ToLower().Contains(s) || l.LastName.ToLower().Contains(s) || l.Company.ToLower().Contains(s) ||
        (l.Email ?? "").ToLower().Contains(s) || (l.Country ?? "").ToLower().Contains(s);

    internal static IQueryable<LeadDto> Project(IQueryable<Lead> q) => q.Select(l => new LeadDto
    {
        Id = l.Id,
        FirstName = l.FirstName,
        LastName = l.LastName,
        Title = l.Title,
        Company = l.Company,
        Email = l.Email,
        Phone = l.Phone,
        City = l.City,
        Country = l.Country,
        Source = l.Source,
        Status = l.Status,
        Score = l.Score,
        EstimatedValue = (double)l.EstimatedValue,
        CampaignId = l.CampaignId,
        CampaignName = l.Campaign == null ? null : l.Campaign.Name,
        OwnerId = l.OwnerId,
        OwnerName = l.Owner == null ? null : l.Owner.FirstName + " " + l.Owner.LastName,
        ConvertedCustomerId = l.ConvertedCustomerId,
        CreatedAt = l.CreatedAt,
        ConvertedAt = l.ConvertedAt,
        LastActivityAt = l.Activities.Max(a => (DateTime?)a.OccurredAt),
        ActivityCount = l.Activities.Count,
    });

    private static IQueryable<object> ProjectActivities(IQueryable<LeadActivity> q) => q.Select(a => new
    {
        a.Id, a.LeadId, a.Type, a.Subject, a.Notes, a.OccurredAt, a.EmployeeId,
        EmployeeName = a.Employee == null ? null : a.Employee.FirstName + " " + a.Employee.LastName,
    });

    private static async Task<IResult?> ValidateRefs(AppDbContext db, int? campaignId, int? ownerId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (campaignId is int c && !await db.Campaigns.AnyAsync(x => x.Id == c, ct)) errors["campaignId"] = ["Campaign not found."];
        if (ownerId is int o && !await db.Employees.AnyAsync(e => e.Id == o, ct)) errors["ownerId"] = ["Employee not found."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static void Apply(Lead l, LeadInput i)
    {
        l.FirstName = i.FirstName.Trim();
        l.LastName = i.LastName.Trim();
        l.Title = i.Title;
        l.Company = i.Company.Trim();
        l.Email = i.Email;
        l.Phone = i.Phone;
        l.City = i.City;
        l.Country = i.Country;
        l.Source = i.Source;
        if (l.Status != i.Status) l.ConvertedAt = i.Status == LeadStatus.Won ? DateTime.Now : null;
        l.Status = i.Status;
        l.Score = i.Score;
        l.EstimatedValue = i.EstimatedValue;
        l.CampaignId = i.CampaignId;
        l.OwnerId = i.OwnerId;
    }
}
