using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Marketing;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class CampaignDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public CampaignChannel Channel { get; init; }
    public CampaignStatus Status { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public double Budget { get; init; }
    public double ActualCost { get; init; }
    public double ExpectedRevenue { get; init; }
    public int Impressions { get; init; }
    public int Clicks { get; init; }
    public double ClickThroughRate { get; init; }
    public int? TargetCategoryId { get; init; }
    public string? TargetCategoryName { get; init; }
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public int LeadCount { get; init; }
    public int WonCount { get; init; }
    public double ConversionRate { get; init; }
    public double WonValue { get; init; }
    public double CostPerLead { get; init; }
    public double RoiPercent { get; init; }
}

public sealed class CampaignInput : IValidatableObject
{
    [Required, StringLength(128)] public string Name { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    public CampaignChannel Channel { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [Range(0, 100_000_000)] public decimal Budget { get; set; }
    [Range(0, 100_000_000)] public decimal ActualCost { get; set; }
    [Range(0, 1_000_000_000)] public decimal ExpectedRevenue { get; set; }
    [Range(0, int.MaxValue)] public int Impressions { get; set; }
    [Range(0, int.MaxValue)] public int Clicks { get; set; }
    public int? TargetCategoryId { get; set; }
    public int? OwnerId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndDate < StartDate) yield return new("End date must be on or after the start date.", [nameof(EndDate)]);
        if (Clicks > Impressions) yield return new("Clicks cannot exceed impressions.", [nameof(Clicks)]);
    }
}

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/campaigns").WithTags("Marketing");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Campaigns).ToPagedResultAsync(q, Search, "-startDate", ct));

        g.MapGet("/lookup", async (AppDbContext db, CancellationToken ct) =>
            await db.Campaigns.OrderByDescending(c => c.StartDate).Select(c => new { c.Id, c.Name, c.Channel, c.Status }).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Campaigns.Where(c => c.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/leads", async (int id, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await LeadEndpoints.Project(db.Leads.Where(l => l.CampaignId == id)).ToPagedResultAsync(q, LeadEndpoints.Search, "-createdAt", ct));

        g.MapGet("/{id:int}/performance", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var campaign = await Project(db.Campaigns.Where(c => c.Id == id)).FirstOrDefaultAsync(ct);
            if (campaign is null) return Results.NotFound();
            var leads = db.Leads.Where(l => l.CampaignId == id);
            var byStatus = await leads.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            var byWeek = await leads.Select(l => l.CreatedAt).ToListAsync(ct);

            return Results.Ok(new
            {
                campaign,
                funnel = new[]
                {
                    new NameValue("Impressions", campaign.Impressions),
                    new NameValue("Clicks", campaign.Clicks),
                    new NameValue("Leads", campaign.LeadCount),
                    new NameValue("Qualified+", byStatus.Where(s => s.Key is LeadStatus.Qualified or LeadStatus.Proposal or LeadStatus.Won).Sum(s => s.Count)),
                    new NameValue("Won", campaign.WonCount),
                },
                leadsByStatus = Enum.GetValues<LeadStatus>().Select(s => new NameValue(s.ToString(), byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0)),
                leadsByWeek = byWeek.GroupBy(d => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7))).OrderBy(g => g.Key)
                    .Select(g => new NameValue(g.Key.ToString("yyyy-MM-dd"), g.Count())),
                budgetUsedPercent = campaign.Budget == 0 ? 0 : Math.Round(campaign.ActualCost / campaign.Budget * 100, 1),
            });
        });

        g.MapPost("/", async (CampaignInput input, AppDbContext db, CancellationToken ct) =>
        {
            if (await ValidateRefs(db, input, ct) is { } problem) return problem;
            var entity = new Campaign { Name = input.Name };
            Apply(entity, input);
            db.Campaigns.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/campaigns/{entity.Id}", await Project(db.Campaigns.Where(c => c.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, CampaignInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Campaigns.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            if (await ValidateRefs(db, input, ct) is { } problem) return problem;
            Apply(entity, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Campaigns.Where(c => c.Id == id)).FirstAsync(ct));
        });

        // Leads keep their history; their CampaignId is cleared.
        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Campaigns.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            db.Campaigns.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    internal static System.Linq.Expressions.Expression<Func<CampaignDto, bool>> Search(string s) => c =>
        c.Name.ToLower().Contains(s) || (c.Description ?? "").ToLower().Contains(s) || (c.OwnerName ?? "").ToLower().Contains(s);

    internal static IQueryable<CampaignDto> Project(IQueryable<Campaign> q) => q.Select(c => new CampaignDto
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        Channel = c.Channel,
        Status = c.Status,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        Budget = (double)c.Budget,
        ActualCost = (double)c.ActualCost,
        ExpectedRevenue = (double)c.ExpectedRevenue,
        Impressions = c.Impressions,
        Clicks = c.Clicks,
        ClickThroughRate = c.Impressions == 0 ? 0 : Math.Round(c.Clicks * 100.0 / c.Impressions, 2),
        TargetCategoryId = c.TargetCategoryId,
        TargetCategoryName = c.TargetCategory == null ? null : c.TargetCategory.Name,
        OwnerId = c.OwnerId,
        OwnerName = c.Owner == null ? null : c.Owner.FirstName + " " + c.Owner.LastName,
        LeadCount = c.Leads.Count,
        WonCount = c.Leads.Count(l => l.Status == LeadStatus.Won),
        ConversionRate = c.Leads.Count == 0 ? 0 : Math.Round(c.Leads.Count(l => l.Status == LeadStatus.Won) * 100.0 / c.Leads.Count, 1),
        WonValue = Math.Round(c.Leads.Where(l => l.Status == LeadStatus.Won).Sum(l => (double)l.EstimatedValue), 2),
        CostPerLead = c.Leads.Count == 0 ? 0 : Math.Round((double)c.ActualCost / c.Leads.Count, 2),
        RoiPercent = c.ActualCost == 0 ? 0
            : Math.Round((c.Leads.Where(l => l.Status == LeadStatus.Won).Sum(l => (double)l.EstimatedValue) - (double)c.ActualCost) / (double)c.ActualCost * 100, 1),
    });

    private static async Task<IResult?> ValidateRefs(AppDbContext db, CampaignInput input, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.TargetCategoryId is int cat && !await db.Categories.AnyAsync(c => c.Id == cat, ct)) errors["targetCategoryId"] = ["Category not found."];
        if (input.OwnerId is int o && !await db.Employees.AnyAsync(e => e.Id == o, ct)) errors["ownerId"] = ["Employee not found."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static void Apply(Campaign c, CampaignInput i)
    {
        c.Name = i.Name.Trim();
        c.Description = i.Description;
        c.Channel = i.Channel;
        c.Status = i.Status;
        c.StartDate = i.StartDate.Date;
        c.EndDate = i.EndDate.Date;
        c.Budget = i.Budget;
        c.ActualCost = i.ActualCost;
        c.ExpectedRevenue = i.ExpectedRevenue;
        c.Impressions = i.Impressions;
        c.Clicks = i.Clicks;
        c.TargetCategoryId = i.TargetCategoryId;
        c.OwnerId = i.OwnerId;
    }
}
