using DispatchApi.Data;
using DispatchApi.Dtos;
using DispatchApi.Infrastructure;
using DispatchApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Services;

/// <summary>SLA risk, dashboard KPIs and the dispatch board.</summary>
public class AnalyticsService(DispatchDbContext db, IClock clock)
{
    /// <summary>
    /// Jobs that matter for a day's SLA picture: everything whose window starts on that day, plus open jobs carried over from
    /// earlier days (overdue backlog).
    /// </summary>
    private async Task<List<JobRow>> DayScopeAsync(DateOnly date, int? regionId, CancellationToken ct)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var q = db.Jobs.AsNoTracking().Where(j => (j.WindowStart >= start && j.WindowStart < end) ||
                                                 (j.WindowStart < start && JobService.OpenStatuses.Contains(j.Status)));
        if (regionId.HasValue) q = q.Where(j => j.RegionId == regionId);
        return await q.Select(JobProjections.ToRow).ToListAsync(ct);
    }

    private static SlaCountsDto Count(IEnumerable<SlaStatusDto> s)
    {
        var list = s.Where(x => x.State != SlaState.NotApplicable).ToList();
        return new(list.Count, list.Count(x => x.State == SlaState.OnTrack), list.Count(x => x.State == SlaState.AtRisk),
            list.Count(x => x.State == SlaState.Breached), list.Count(x => x.State == SlaState.Met));
    }

    public async Task<SlaSummaryDto> SlaSummaryAsync(DateOnly? date, int? regionId, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var now = clock.Now;
        var rows = await DayScopeAsync(d, regionId, ct);
        var withSla = rows.Select(r => (Row: r, Sla: r.Sla(now))).Where(x => x.Sla.State != SlaState.NotApplicable).ToList();
        var counts = Count(withSla.Select(x => x.Sla));
        double Exposure(IEnumerable<(JobRow Row, SlaStatusDto Sla)> xs) =>
            Math.Round(xs.Where(x => x.Sla.State is SlaState.AtRisk or SlaState.Breached).Sum(x => x.Row.SlaPenalty), 2);

        List<SlaGroupDto> Group(Func<JobRow, string> key, Func<JobRow, string> label, Func<IGrouping<string, (JobRow Row, SlaStatusDto Sla)>, object>? order = null)
        {
            var g = withSla.GroupBy(x => key(x.Row));
            var ordered = order == null ? g.OrderBy(x => x.Key) : g.OrderBy(order);
            return ordered.Select(x => new SlaGroupDto(x.Key, label(x.First().Row), Count(x.Select(y => y.Sla)), Exposure(x))).ToList();
        }

        var dayStart = d.ToDateTime(TimeOnly.MinValue);
        var byHour = Enumerable.Range(0, 24).Select(h =>
        {
            var inHour = withSla.Where(x => x.Sla.DueAt is { } due && x.Sla.Stage != SlaStage.Closed && due >= dayStart.AddHours(h) && due < dayStart.AddHours(h + 1)).ToList();
            return new SlaRiskHourDto(h, inHour.Count, inHour.Count(x => x.Sla.State == SlaState.AtRisk), inHour.Count(x => x.Sla.State == SlaState.Breached));
        }).Where(h => h.Due > 0).ToList();

        var compliance = counts.Total == 0 ? 100 : Math.Round((counts.Total - counts.Breached) * 100.0 / counts.Total, 1);
        return new SlaSummaryDto(d, now, regionId, counts, compliance, Exposure(withSla),
            Group(r => r.PriorityRank.ToString(), r => r.Priority.ToString()),
            Group(r => r.RegionId.ToString(), r => r.RegionName, g => g.First().Row.RegionName),
            Group(r => r.CustomerTier.ToString(), r => r.CustomerTier.ToString(), g => g.Key switch { "Platinum" => 0, "Gold" => 1, _ => 2 }),
            byHour);
    }

    public async Task<IReadOnlyList<JobListItemDto>> SlaAtRiskAsync(DateOnly? date, int? regionId, List<SlaState>? states, JobPriority? priority,
        int limit, CancellationToken ct)
    {
        var now = clock.Now;
        var wanted = states is { Count: > 0 } ? states : [SlaState.AtRisk, SlaState.Breached];
        var rows = await DayScopeAsync(date ?? clock.Today, regionId, ct);
        return rows.Where(r => (priority == null || r.Priority == priority) && wanted.Contains(r.Sla(now).State))
            .OrderBySlaUrgency(now).Take(Math.Clamp(limit, 1, 2000)).Select(r => r.ToDto(now)).ToList();
    }

    public async Task<IReadOnlyList<SlaTrendPointDto>> SlaTrendAsync(int days, int? regionId, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 90);
        var to = clock.Today;
        var from = to.AddDays(-days + 1).ToDateTime(TimeOnly.MinValue);
        var q = db.Jobs.AsNoTracking().Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from);
        if (regionId.HasValue) q = q.Where(j => j.RegionId == regionId);
        var rows = await q.Select(j => new { j.ActualStart, j.ActualEnd, j.ResponseDueAt, j.ResolutionDueAt }).ToListAsync(ct);
        return Enumerable.Range(0, days).Select(i =>
        {
            var d = to.AddDays(-days + 1 + i);
            var day = rows.Where(r => DateOnly.FromDateTime(r.ActualEnd!.Value) == d).ToList();
            var resp = day.Count(r => r.ActualStart <= r.ResponseDueAt);
            var reso = day.Count(r => r.ActualEnd <= r.ResolutionDueAt);
            var breaches = day.Count(r => r.ActualStart > r.ResponseDueAt || r.ActualEnd > r.ResolutionDueAt);
            return new SlaTrendPointDto(d, day.Count, resp, reso, day.Count == 0 ? 0 : Math.Round(resp * 100.0 / day.Count, 1),
                day.Count == 0 ? 0 : Math.Round(reso * 100.0 / day.Count, 1), breaches);
        }).ToList();
    }

    // ------------------------------------------------------------------ dashboard
    private async Task<List<(Shift Shift, Technician Tech)>> ShiftsOnAsync(DateOnly d, int? regionId, CancellationToken ct)
    {
        var q = db.Shifts.AsNoTracking().Include(s => s.Technician).ThenInclude(t => t.Depot)
            .Where(s => s.ShiftDate == d && s.Status != ShiftStatus.Absent && s.Status != ShiftStatus.Cancelled);
        if (regionId.HasValue) q = q.Where(s => s.Technician.Depot.RegionId == regionId);
        return (await q.ToListAsync(ct)).Select(s => (s, s.Technician)).ToList();
    }

    public async Task<KpiDto> KpisAsync(DateOnly? date, int? regionId, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var now = clock.Now;
        var start = d.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var dayJobs = db.Jobs.AsNoTracking().Where(j => j.WindowStart >= start && j.WindowStart < end);
        if (regionId.HasValue) dayJobs = dayJobs.Where(j => j.RegionId == regionId);
        var statusCounts = await dayJobs.GroupBy(j => j.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        int C(params JobStatus[] s) => s.Sum(x => statusCounts.GetValueOrDefault(x));

        var shifts = await ShiftsOnAsync(d, regionId, ct);
        var shiftMinutes = shifts.Sum(s => (s.Shift.EndTime - s.Shift.StartTime).TotalMinutes);
        var booked = await dayJobs.Where(j => j.AssignedTechnicianId != null && j.Status != JobStatus.Cancelled).SumAsync(j => j.EstimatedDurationMinutes, ct);
        var techIds = shifts.Select(s => s.Tech.Id).ToHashSet();
        var field = shifts.GroupBy(s => s.Tech.FieldStatus).ToDictionary(g => g.Key, g => g.Count());

        var from30 = start.AddDays(-30);
        var hist = db.Jobs.AsNoTracking().Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from30 && j.ActualEnd < end);
        if (regionId.HasValue) hist = hist.Where(j => j.RegionId == regionId);
        var h = await hist.Select(j => new { j.FirstTimeFix, j.CustomerRating, j.ActualStart, j.ActualEnd, j.ResponseDueAt }).ToListAsync(ct);

        var scope = await DayScopeAsync(d, regionId, ct);
        var slas = scope.Select(r => r.Sla(now).State).ToList();
        var resched = db.Reschedules.AsNoTracking().Where(r => r.CreatedAt >= start && r.CreatedAt < end);
        if (regionId.HasValue) resched = resched.Where(r => r.Job.RegionId == regionId);

        return new KpiDto(d, now, regionId, statusCounts.Values.Sum(), C(JobStatus.Completed),
            C(JobStatus.Assigned, JobStatus.Dispatched, JobStatus.EnRoute, JobStatus.OnSite), C(JobStatus.Unassigned), C(JobStatus.OnHold),
            C(JobStatus.Cancelled), techIds.Count, field.GetValueOrDefault(FieldStatus.Available), field.GetValueOrDefault(FieldStatus.OnSite),
            field.GetValueOrDefault(FieldStatus.EnRoute), shiftMinutes == 0 ? 0 : Math.Round(booked * 100.0 / shiftMinutes, 1),
            h.Count == 0 ? 0 : Math.Round(h.Count(x => x.FirstTimeFix == true) * 100.0 / h.Count, 1),
            h.Where(x => x.CustomerRating.HasValue).Select(x => (double)x.CustomerRating!.Value).DefaultIfEmpty(0).Average() is var avg ? Math.Round(avg, 2) : 0,
            h.Count == 0 ? 0 : Math.Round(h.Count(x => x.ActualStart <= x.ResponseDueAt) * 100.0 / h.Count, 1),
            slas.Count(s => s == SlaState.AtRisk), slas.Count(s => s == SlaState.Breached),
            h.Count == 0 ? 0 : Math.Round(h.Average(x => (x.ActualEnd!.Value - x.ActualStart!.Value).TotalMinutes), 0),
            await resched.CountAsync(ct));
    }

    public async Task<IReadOnlyList<HourBucketDto>> JobsByHourAsync(DateOnly? date, int? regionId, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var start = d.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var q = db.Jobs.AsNoTracking().Where(j => j.WindowStart >= start && j.WindowStart < end && j.Status != JobStatus.Cancelled);
        if (regionId.HasValue) q = q.Where(j => j.RegionId == regionId);
        var rows = await q.Select(j => new { At = j.ScheduledStart ?? j.WindowStart, j.Status }).ToListAsync(ct);
        return Enumerable.Range(5, 16).Select(hr =>
        {
            var x = rows.Where(r => r.At.Hour == hr).ToList();
            return new HourBucketDto(hr, x.Count, x.Count(r => r.Status == JobStatus.Completed), x.Count(r => r.Status == JobStatus.Unassigned));
        }).ToList();
    }

    public async Task<IReadOnlyList<StatusCountDto>> StatusBreakdownAsync(DateOnly? date, int? regionId, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var start = d.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var q = db.Jobs.AsNoTracking().Where(j => j.WindowStart >= start && j.WindowStart < end);
        if (regionId.HasValue) q = q.Where(j => j.RegionId == regionId);
        var counts = await q.GroupBy(j => j.Status).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        return Enum.GetValues<JobStatus>().Select(s => new StatusCountDto(s.ToString(), counts.FirstOrDefault(c => c.Key == s)?.C ?? 0)).ToList();
    }

    public async Task<IReadOnlyList<StatusCountDto>> JobsByCategoryAsync(DateOnly? date, int? regionId, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var start = d.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var q = db.Jobs.AsNoTracking().Where(j => j.WindowStart >= start && j.WindowStart < end && j.Status != JobStatus.Cancelled);
        if (regionId.HasValue) q = q.Where(j => j.RegionId == regionId);
        var counts = await q.GroupBy(j => j.JobType.Category).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        return counts.OrderByDescending(c => c.C).Select(c => new StatusCountDto(c.Key.ToString(), c.C)).ToList();
    }

    public async Task<IReadOnlyList<TechUtilizationDto>> TechUtilizationAsync(DateOnly? date, int? regionId, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var start = d.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var shifts = await ShiftsOnAsync(d, regionId, ct);
        var ids = shifts.Select(s => s.Tech.Id).ToList();
        var jobs = await db.Jobs.AsNoTracking()
            .Where(j => j.AssignedTechnicianId != null && ids.Contains(j.AssignedTechnicianId.Value) && j.ScheduledStart >= start && j.ScheduledStart < end && j.Status != JobStatus.Cancelled)
            .GroupBy(j => j.AssignedTechnicianId!.Value).Select(g => new { Id = g.Key, Jobs = g.Count(), Minutes = g.Sum(x => x.EstimatedDurationMinutes) })
            .ToDictionaryAsync(x => x.Id, ct);
        var regions = await db.Regions.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        return shifts.Select(s =>
        {
            var mins = (int)(s.Shift.EndTime - s.Shift.StartTime).TotalMinutes;
            var j = jobs.GetValueOrDefault(s.Tech.Id);
            var booked = j?.Minutes ?? 0;
            return new TechUtilizationDto(s.Tech.Id, $"{s.Tech.FirstName} {s.Tech.LastName}", regions[s.Tech.Depot.RegionId], j?.Jobs ?? 0, booked, mins,
                mins == 0 ? 0 : Math.Round(booked * 100.0 / mins, 1));
        }).OrderByDescending(x => x.UtilizationPct).ToList();
    }

    // ------------------------------------------------------------------ dispatch board
    public async Task<DispatchBoardDto> BoardAsync(DateOnly? date, int? regionId, int? depotId, int? skillId, bool includeOffShift, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var now = clock.Now;
        var start = d.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);

        var techQ = db.Technicians.AsNoTracking().Include(t => t.Depot).Include(t => t.Skills).ThenInclude(s => s.Skill)
            .Where(t => t.EmploymentStatus != EmploymentStatus.Inactive);
        if (regionId.HasValue) techQ = techQ.Where(t => t.Depot.RegionId == regionId);
        if (depotId.HasValue) techQ = techQ.Where(t => t.DepotId == depotId);
        if (skillId.HasValue) techQ = techQ.Where(t => t.Skills.Any(s => s.SkillId == skillId));
        var techs = await techQ.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToListAsync(ct);
        var ids = techs.Select(t => t.Id).ToList();

        var shifts = (await db.Shifts.AsNoTracking().Where(s => ids.Contains(s.TechnicianId) && s.ShiftDate == d).ToListAsync(ct))
            .GroupBy(s => s.TechnicianId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.Status is ShiftStatus.Absent or ShiftStatus.Cancelled).First());
        var off = (await db.TimeOffRequests.AsNoTracking().Where(t => ids.Contains(t.TechnicianId) && t.Status == TimeOffStatus.Approved &&
                                                                      t.StartDate <= d && t.EndDate >= d).Select(t => t.TechnicianId).ToListAsync(ct)).ToHashSet();
        var jobRows = await db.Jobs.AsNoTracking()
            .Where(j => j.AssignedTechnicianId != null && ids.Contains(j.AssignedTechnicianId.Value) && j.ScheduledStart >= start && j.ScheduledStart < end && j.Status != JobStatus.Cancelled)
            .Select(JobProjections.ToRow).ToListAsync(ct);
        var jobsByTech = jobRows.GroupBy(r => r.AssignedTechnicianId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(r => r.ScheduledStart).ToList());

        var board = new List<BoardTechnicianDto>();
        foreach (var t in techs)
        {
            shifts.TryGetValue(t.Id, out var s);
            var tj = jobsByTech.GetValueOrDefault(t.Id) ?? [];
            if (!includeOffShift && s == null && tj.Count == 0) continue;
            var sStart = s == null ? (DateTime?)null : d.ToDateTime(s.StartTime);
            var sEnd = s == null ? (DateTime?)null : d.ToDateTime(s.EndTime);
            var booked = tj.Sum(j => j.EstimatedDurationMinutes);
            var mins = s == null ? 0 : (s.EndTime - s.StartTime).TotalMinutes;
            board.Add(new BoardTechnicianDto(t.Id, $"{t.FirstName} {t.LastName}", t.Level, t.FieldStatus, t.DepotId, t.Depot.Name,
                string.Join(", ", t.Skills.Select(x => x.Skill.Code)), sStart, sEnd, s?.Status, off.Contains(t.Id), booked,
                mins == 0 ? 0 : Math.Round(booked * 100.0 / mins, 1), tj.Select(r => r.ToDto(now)).ToList()));
        }

        var unq = db.Jobs.AsNoTracking().Where(j => j.Status == JobStatus.Unassigned && j.WindowStart < end &&
                                                     (j.WindowStart >= start || d == clock.Today));
        if (regionId.HasValue) unq = unq.Where(j => j.RegionId == regionId);
        if (skillId.HasValue) unq = unq.Where(j => j.JobType.RequiredSkillId == skillId);
        var unassigned = (await unq.Select(JobProjections.ToRow).ToListAsync(ct)).OrderBySlaUrgency(now).Select(r => r.ToDto(now)).ToList();
        return new DispatchBoardDto(d, now, regionId, board, unassigned);
    }
}
