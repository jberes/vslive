using System.Linq.Expressions;
using DispatchApi.Dtos;
using DispatchApi.Models;

namespace DispatchApi.Services;

/// <summary>Flat, EF-translatable projection of a job. Sorting on job lists uses these property names.</summary>
public class JobRow
{
    public int Id { get; init; }
    public string JobNumber { get; init; } = "";
    public JobStatus Status { get; init; }
    public JobPriority Priority { get; init; }
    public int PriorityRank { get; init; }
    public string Summary { get; init; } = "";
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public CustomerTier CustomerTier { get; init; }
    public int SiteId { get; init; }
    public string SiteName { get; init; } = "";
    public string SiteCity { get; init; } = "";
    public double SiteLatitude { get; init; }
    public double SiteLongitude { get; init; }
    public int RegionId { get; init; }
    public string RegionName { get; init; } = "";
    public int JobTypeId { get; init; }
    public string JobTypeName { get; init; } = "";
    public JobCategory JobCategory { get; init; }
    public int RequiredSkillId { get; init; }
    public string RequiredSkillName { get; init; } = "";
    public DateTime WindowStart { get; init; }
    public DateTime WindowEnd { get; init; }
    public DateTime? ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public DateTime ResponseDueAt { get; init; }
    public DateTime ResolutionDueAt { get; init; }
    public int? AssignedTechnicianId { get; init; }
    public string? AssignedTechnicianName { get; init; }
    public DateTime? ActualStart { get; init; }
    public DateTime? ActualEnd { get; init; }
    public int RescheduleCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int SlaThresholdMinutes { get; init; }
    public double SlaPenalty { get; init; }
}

public static class JobProjections
{
    public static readonly Expression<Func<Job, JobRow>> ToRow = j => new JobRow
    {
        Id = j.Id,
        JobNumber = j.JobNumber,
        Status = j.Status,
        Priority = j.Priority,
        PriorityRank = j.Priority == JobPriority.Critical ? 0 : j.Priority == JobPriority.High ? 1 : j.Priority == JobPriority.Medium ? 2 : 3,
        Summary = j.Summary,
        CustomerId = j.CustomerId,
        CustomerName = j.Customer.Name,
        CustomerTier = j.Customer.Tier,
        SiteId = j.SiteId,
        SiteName = j.Site.Name,
        SiteCity = j.Site.City,
        SiteLatitude = j.Site.Latitude,
        SiteLongitude = j.Site.Longitude,
        RegionId = j.RegionId,
        RegionName = j.Region.Name,
        JobTypeId = j.JobTypeId,
        JobTypeName = j.JobType.Name,
        JobCategory = j.JobType.Category,
        RequiredSkillId = j.JobType.RequiredSkillId,
        RequiredSkillName = j.JobType.RequiredSkill.Name,
        WindowStart = j.WindowStart,
        WindowEnd = j.WindowEnd,
        ScheduledStart = j.ScheduledStart,
        ScheduledEnd = j.ScheduledEnd,
        EstimatedDurationMinutes = j.EstimatedDurationMinutes,
        ResponseDueAt = j.ResponseDueAt,
        ResolutionDueAt = j.ResolutionDueAt,
        AssignedTechnicianId = j.AssignedTechnicianId,
        AssignedTechnicianName = j.AssignedTechnician == null ? null : j.AssignedTechnician.FirstName + " " + j.AssignedTechnician.LastName,
        ActualStart = j.ActualStart,
        ActualEnd = j.ActualEnd,
        RescheduleCount = j.RescheduleCount,
        CreatedAt = j.CreatedAt,
        UpdatedAt = j.UpdatedAt,
        SlaThresholdMinutes = j.SlaPolicy.AtRiskThresholdMinutes,
        SlaPenalty = j.SlaPolicy.PenaltyAmount,
    };

    public static SlaStatusDto Sla(this JobRow r, DateTime now) =>
        SlaCalculator.Compute(r.Status, r.ScheduledStart, r.EstimatedDurationMinutes, r.ResponseDueAt, r.ResolutionDueAt,
            r.ActualStart, r.ActualEnd, r.SlaThresholdMinutes, now);

    public static JobListItemDto ToDto(this JobRow r, DateTime now) => new(
        r.Id, r.JobNumber, r.Status, r.Priority, r.Summary, r.CustomerId, r.CustomerName, r.CustomerTier, r.SiteId, r.SiteName,
        r.SiteCity, r.SiteLatitude, r.SiteLongitude, r.RegionId, r.RegionName, r.JobTypeId, r.JobTypeName, r.JobCategory,
        r.RequiredSkillId, r.RequiredSkillName, r.WindowStart, r.WindowEnd, r.ScheduledStart, r.ScheduledEnd,
        r.EstimatedDurationMinutes, r.ResponseDueAt, r.ResolutionDueAt, r.AssignedTechnicianId, r.AssignedTechnicianName,
        r.ActualStart, r.ActualEnd, r.RescheduleCount, r.CreatedAt, r.UpdatedAt, r.Sla(now));

    /// <summary>Maps API sort aliases to JobRow fields (e.g. "priority" sorts by severity, not alphabetically).</summary>
    public static string? NormalizeSort(string? sort) => sort is null ? null : string.Join(',',
        sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s =>
        {
            var desc = s.StartsWith('-');
            var f = s.TrimStart('-', '+');
            f = f.ToLowerInvariant() switch
            {
                "priority" => "PriorityRank",
                "technician" or "assignedtechnician" => "AssignedTechnicianName",
                "customer" => "CustomerName",
                "site" => "SiteName",
                "region" => "RegionName",
                "jobtype" => "JobTypeName",
                _ => f,
            };
            return (desc ? "-" : "") + f;
        }));

    public static IOrderedEnumerable<JobRow> OrderBySlaUrgency(this IEnumerable<JobRow> rows, DateTime now) =>
        rows.OrderBy(r => r.Sla(now).State switch { SlaState.Breached => 0, SlaState.AtRisk => 1, SlaState.OnTrack => 2, _ => 3 })
            .ThenBy(r => r.PriorityRank)
            .ThenBy(r => r.Sla(now).MinutesRemaining ?? int.MaxValue);
}
