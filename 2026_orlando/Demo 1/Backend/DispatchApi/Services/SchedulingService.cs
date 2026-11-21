using DispatchApi.Data;
using DispatchApi.Dtos;
using DispatchApi.Infrastructure;
using DispatchApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Services;

/// <summary>
/// Assignment validation, technician recommendations, assign / unassign, reschedule, and whole-day technician moves.
/// </summary>
public class SchedulingService(DispatchDbContext db, IClock clock, JobService jobs)
{
    private const double AvgSpeedKmPerMinute = 0.75; // ~45 km/h urban average
    private const int SlotGranularity = 15;
    private const int TravelBufferMinutes = 10;
    /// <summary>Overtime a dispatcher may book past shift end before it becomes a blocking issue.</summary>
    private const int MaxOvertimeMinutes = 180;

    private static readonly JobStatus[] Active =
        [JobStatus.Assigned, JobStatus.Dispatched, JobStatus.EnRoute, JobStatus.OnSite, JobStatus.OnHold];

    // ------------------------------------------------------------------ technician day context
    private record TechDay(Technician Tech, Shift? Shift, bool OnTimeOff, List<Job> Jobs)
    {
        public DateTime? ShiftStart => Shift == null ? null : Shift.ShiftDate.ToDateTime(Shift.StartTime);
        public DateTime? ShiftEnd => Shift == null ? null : Shift.ShiftDate.ToDateTime(Shift.EndTime);
        public bool Working => Shift is { Status: not (ShiftStatus.Absent or ShiftStatus.Cancelled) } && !OnTimeOff;
    }

    private async Task<Dictionary<int, TechDay>> LoadTechDaysAsync(IReadOnlyCollection<Technician> techs, DateOnly date, CancellationToken ct)
    {
        var ids = techs.Select(t => t.Id).ToList();
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var shifts = await db.Shifts.AsNoTracking().Where(s => ids.Contains(s.TechnicianId) && s.ShiftDate == date).ToListAsync(ct);
        var offs = await db.TimeOffRequests.AsNoTracking()
            .Where(t => ids.Contains(t.TechnicianId) && t.Status == TimeOffStatus.Approved && t.StartDate <= date && t.EndDate >= date)
            .Select(t => t.TechnicianId).ToListAsync(ct);
        var dayJobs = await db.Jobs.AsNoTracking()
            .Where(j => j.AssignedTechnicianId != null && ids.Contains(j.AssignedTechnicianId.Value) && j.ScheduledStart >= dayStart &&
                        j.ScheduledStart < dayEnd && j.Status != JobStatus.Cancelled)
            .Include(j => j.Site)
            .ToListAsync(ct);
        return techs.ToDictionary(t => t.Id, t => new TechDay(t,
            shifts.Where(s => s.TechnicianId == t.Id).OrderBy(s => s.Status is ShiftStatus.Absent or ShiftStatus.Cancelled).FirstOrDefault(),
            offs.Contains(t.Id),
            dayJobs.Where(j => j.AssignedTechnicianId == t.Id).OrderBy(j => j.ScheduledStart).ToList()));
    }

    private static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        var dx = (lng1 - lng2) * 111.0 * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180);
        var dy = (lat1 - lat2) * 111.0;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static int TravelMinutes(double km) => (int)Math.Ceiling(km / AvgSpeedKmPerMinute) + TravelBufferMinutes;

    private static DateTime RoundUp(DateTime t)
    {
        var rem = t.Minute % SlotGranularity;
        var r = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
        return rem == 0 && t.Second == 0 ? r : r.AddMinutes(SlotGranularity - rem);
    }

    /// <summary>Earliest start in the technician's day where the job fits between existing jobs (with travel).</summary>
    private DateTime? EarliestStart(TechDay day, Job job, int travelMinutes, int? excludeJobId)
    {
        if (day.ShiftStart is null) return null;
        var now = clock.Now;
        var candidate = RoundUp(Max(job.WindowStart, day.ShiftStart.Value, now.Date == job.WindowStart.Date ? now.AddMinutes(travelMinutes) : DateTime.MinValue));
        var latestEnd = day.ShiftEnd!.Value.AddMinutes(MaxOvertimeMinutes);
        foreach (var other in day.Jobs.Where(j => j.Id != excludeJobId && j.ScheduledStart.HasValue))
        {
            var oStart = other.ScheduledStart!.Value;
            var oEnd = (other.Status == JobStatus.OnSite && other.ActualStart.HasValue
                ? Max(other.ActualStart.Value.AddMinutes(other.EstimatedDurationMinutes), now.AddMinutes(15))
                : other.ScheduledEnd ?? oStart.AddMinutes(other.EstimatedDurationMinutes));
            if (other.Status == JobStatus.Completed && other.ActualEnd.HasValue) oEnd = other.ActualEnd.Value;
            var fitsBefore = candidate.AddMinutes(job.EstimatedDurationMinutes + TravelBufferMinutes) <= oStart;
            if (fitsBefore) break;
            if (oEnd.AddMinutes(TravelBufferMinutes) > candidate) candidate = RoundUp(oEnd.AddMinutes(travelMinutes));
        }
        return candidate.AddMinutes(job.EstimatedDurationMinutes) <= latestEnd ? candidate : null;
    }

    private static DateTime Max(params DateTime[] values) => values.Max();

    // ------------------------------------------------------------------ validation
    private List<AssignmentIssueDto> Validate(Job job, TechDay day, DateTime start, int? excludeJobId)
    {
        var issues = new List<AssignmentIssueDto>();
        var t = day.Tech;
        var end = start.AddMinutes(job.EstimatedDurationMinutes);
        void Block(string code, string msg) => issues.Add(new(code, IssueSeverity.Blocking, msg));
        void Warn(string code, string msg) => issues.Add(new(code, IssueSeverity.Warning, msg));

        if (!job.IsOpen) Block("JOB_CLOSED", $"Job is {job.Status}.");
        if (job.Status is JobStatus.OnSite) Block("JOB_IN_PROGRESS", "Technician is already on site; complete or hold the job first.");
        if (t.EmploymentStatus != EmploymentStatus.Active) Block("TECH_NOT_ACTIVE", $"Technician is {t.EmploymentStatus}.");

        var skill = t.Skills.FirstOrDefault(s => s.SkillId == job.JobType.RequiredSkillId);
        if (skill == null) Block("SKILL_MISSING", $"Technician lacks required skill '{job.JobType.RequiredSkill?.Name ?? job.JobType.RequiredSkillId.ToString()}'.");
        else
        {
            if (skill.Proficiency < job.JobType.MinProficiency)
                Block("PROFICIENCY_LOW", $"Proficiency {skill.Proficiency} is below the required {job.JobType.MinProficiency}.");
            if (skill.CertifiedUntil.HasValue && skill.CertifiedUntil < DateOnly.FromDateTime(start))
                Block("CERT_EXPIRED", $"Certification expired on {skill.CertifiedUntil:yyyy-MM-dd}.");
        }

        if (day.OnTimeOff) Block("TIME_OFF", "Technician has approved time off on this date.");
        if (day.Shift == null) Block("NO_SHIFT", "Technician has no shift on this date.");
        else if (day.Shift.Status is ShiftStatus.Absent or ShiftStatus.Cancelled) Block("SHIFT_" + day.Shift.Status.ToString().ToUpperInvariant(), $"Shift is {day.Shift.Status}.");
        else
        {
            if (start < day.ShiftStart) Warn("BEFORE_SHIFT", $"Starts before shift start {day.ShiftStart:HH:mm}.");
            if (end > day.ShiftEnd!.Value.AddMinutes(MaxOvertimeMinutes)) Block("AFTER_SHIFT", $"Ends at {end:HH:mm}, more than {MaxOvertimeMinutes / 60}h past shift end {day.ShiftEnd:HH:mm}.");
            else if (end > day.ShiftEnd) Warn("OVERTIME", $"Ends at {end:HH:mm}, after shift end {day.ShiftEnd:HH:mm} (overtime).");
        }

        var overlap = day.Jobs.FirstOrDefault(o => o.Id != excludeJobId && o.Status != JobStatus.Completed && o.ScheduledStart.HasValue &&
                                                   o.ScheduledStart < end && (o.ScheduledEnd ?? o.ScheduledStart.Value.AddMinutes(o.EstimatedDurationMinutes)) > start);
        if (overlap != null) Block("OVERLAP", $"Overlaps {overlap.JobNumber} ({overlap.ScheduledStart:HH:mm}-{overlap.ScheduledEnd:HH:mm}).");

        var count = day.Jobs.Count(o => o.Id != excludeJobId);
        if (count >= t.MaxJobsPerDay) Warn("CAPACITY", $"Technician already has {count} jobs (max {t.MaxJobsPerDay}).");
        if (t.Depot.RegionId != job.RegionId) Warn("OUT_OF_REGION", "Technician is based outside the job's region.");
        if (start > job.ResponseDueAt) Warn("SLA_RESPONSE", $"Start {start:HH:mm} is after the response SLA due {job.ResponseDueAt:MM-dd HH:mm}.");
        if (end > job.ResolutionDueAt) Warn("SLA_RESOLUTION", $"Projected finish is after the resolution SLA due {job.ResolutionDueAt:MM-dd HH:mm}.");
        if (start < job.WindowStart || start > job.WindowEnd) Warn("OUTSIDE_WINDOW", $"Start is outside the customer window {job.WindowStart:HH:mm}-{job.WindowEnd:HH:mm}.");
        if (start < clock.Now.AddMinutes(-5)) Warn("START_IN_PAST", "Scheduled start is in the past.");
        return issues;
    }

    private async Task<Technician> LoadTechAsync(int id, CancellationToken ct) =>
        await db.Technicians.Include(t => t.Skills).Include(t => t.Depot).SingleOrDefaultAsync(t => t.Id == id, ct)
        ?? throw ApiException.NotFound("Technician", id);

    private async Task<Job> LoadJobAsync(int id, CancellationToken ct) =>
        await db.Jobs.Include(j => j.JobType).ThenInclude(jt => jt.RequiredSkill).Include(j => j.Site).Include(j => j.SlaPolicy)
            .SingleOrDefaultAsync(j => j.Id == id, ct) ?? throw ApiException.NotFound("Job", id);

    private async Task<(AssignmentValidationDto Validation, TechDay Day)> BuildValidationAsync(Job job, Technician tech, DateTime? requestedStart, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(requestedStart ?? job.ScheduledStart ?? job.WindowStart);
        var day = (await LoadTechDaysAsync([tech], date, ct))[tech.Id];
        var origin = OriginFor(day, job, requestedStart);
        var travel = TravelMinutes(DistanceKm(origin.lat, origin.lng, job.Site.Latitude, job.Site.Longitude));
        var start = requestedStart ?? EarliestStart(day, job, travel, job.Id) ?? RoundUp(Max(job.WindowStart, clock.Now));
        var issues = Validate(job, day, start, job.Id);
        if (requestedStart == null && EarliestStart(day, job, travel, job.Id) == null && day.Working)
            issues.Add(new("NO_FREE_SLOT", IssueSeverity.Blocking, "No free slot remains in the technician's shift for this job."));
        return (new AssignmentValidationDto(issues.All(i => i.Severity != IssueSeverity.Blocking), start,
            start.AddMinutes(job.EstimatedDurationMinutes), issues), day);
    }

    private (double lat, double lng) OriginFor(TechDay day, Job job, DateTime? at)
    {
        // Leave from the previous job's site if there is one before the slot, otherwise current location (today) or depot.
        var pivot = at ?? job.WindowStart;
        var prev = day.Jobs.Where(j => j.Id != job.Id && j.ScheduledStart < pivot).OrderByDescending(j => j.ScheduledStart).FirstOrDefault();
        if (prev != null) return (prev.Site.Latitude, prev.Site.Longitude);
        if (DateOnly.FromDateTime(pivot) == clock.Today && day.Tech.CurrentLatitude.HasValue)
            return (day.Tech.CurrentLatitude.Value, day.Tech.CurrentLongitude!.Value);
        return (day.Tech.Depot.Latitude, day.Tech.Depot.Longitude);
    }

    public async Task<AssignmentValidationDto> ValidateAsync(int jobId, int techId, DateTime? start, CancellationToken ct)
    {
        var job = await LoadJobAsync(jobId, ct);
        var tech = await LoadTechAsync(techId, ct);
        return (await BuildValidationAsync(job, tech, start, ct)).Validation;
    }

    // ------------------------------------------------------------------ recommendations
    public async Task<IReadOnlyList<TechnicianRecommendationDto>> RecommendAsync(int jobId, int limit, bool includeIneligible,
        int? excludeTechnicianId, bool crossRegion, CancellationToken ct)
    {
        var job = await LoadJobAsync(jobId, ct);
        var date = DateOnly.FromDateTime(job.ScheduledStart ?? job.WindowStart);
        var skillId = job.JobType.RequiredSkillId;
        var techQuery = db.Technicians.AsNoTracking().Include(t => t.Skills).Include(t => t.Depot)
            .Where(t => t.EmploymentStatus == EmploymentStatus.Active && t.Skills.Any(s => s.SkillId == skillId));
        if (!crossRegion) techQuery = techQuery.Where(t => t.Depot.RegionId == job.RegionId);
        if (excludeTechnicianId.HasValue) techQuery = techQuery.Where(t => t.Id != excludeTechnicianId);
        var techs = await techQuery.ToListAsync(ct);
        var days = await LoadTechDaysAsync(techs, date, ct);

        var results = new List<TechnicianRecommendationDto>();
        foreach (var t in techs)
        {
            var day = days[t.Id];
            var origin = OriginFor(day, job, null);
            var km = DistanceKm(origin.lat, origin.lng, job.Site.Latitude, job.Site.Longitude);
            var travel = TravelMinutes(km);
            var earliest = day.Working ? EarliestStart(day, job, travel, job.Id) : null;
            var start = earliest ?? RoundUp(Max(job.WindowStart, clock.Now));
            var issues = Validate(job, day, start, job.Id);
            if (day.Working && earliest == null)
                issues.Add(new("NO_FREE_SLOT", IssueSeverity.Blocking, "No free slot remains in the technician's shift."));
            var blocked = issues.Any(i => i.Severity == IssueSeverity.Blocking);
            if (blocked && !includeIneligible) continue;

            var prof = t.Skills.First(s => s.SkillId == skillId).Proficiency;
            var booked = day.Jobs.Where(j => j.Id != job.Id).Sum(j => j.EstimatedDurationMinutes);
            var shiftMinutes = day.ShiftStart.HasValue ? (day.ShiftEnd!.Value - day.ShiftStart.Value).TotalMinutes : 0;
            var util = shiftMinutes > 0 ? Math.Round(booked / shiftMinutes * 100, 1) : 0;
            var meetsSla = earliest.HasValue && earliest <= job.ResponseDueAt;
            var waitMinutes = earliest.HasValue ? Math.Max(0, (earliest.Value - job.WindowStart).TotalMinutes) : 600;
            var score = 100 - travel * 0.35 - waitMinutes * 0.12 + prof * 4 - util * 0.15 + t.Rating * 2
                        - (meetsSla ? 0 : 35) - issues.Count(i => i.Severity == IssueSeverity.Warning) * 3 - (blocked ? 100 : 0)
                        - (earliest.HasValue && day.ShiftEnd.HasValue ? Math.Max(0, (earliest.Value.AddMinutes(job.EstimatedDurationMinutes) - day.ShiftEnd.Value).TotalMinutes) * 0.25 : 0);

            results.Add(new TechnicianRecommendationDto(t.Id, $"{t.FirstName} {t.LastName}", t.Level, t.FieldStatus, t.Depot.Name, prof,
                Math.Round(km, 1), travel, day.Jobs.Count(j => j.Id != job.Id), t.MaxJobsPerDay, util, day.ShiftStart, day.ShiftEnd,
                earliest, meetsSla, Math.Round(score, 1), issues));
        }
        return results.OrderByDescending(r => r.Score).Take(Math.Clamp(limit, 1, 100)).ToList();
    }

    // ------------------------------------------------------------------ assign / unassign
    public async Task<AssignmentResultDto> AssignAsync(int jobId, AssignJobDto dto, CancellationToken ct)
    {
        var job = await LoadJobAsync(jobId, ct);
        var tech = await LoadTechAsync(dto.TechnicianId, ct);
        var (validation, _) = await BuildValidationAsync(job, tech, dto.ScheduledStart, ct);
        if (!validation.CanAssign && !dto.Force)
            throw ApiException.Conflict("Assignment has blocking issues. Resolve them or resend with force=true.", validation);

        var now = clock.Now;
        var previousTech = job.AssignedTechnicianId;
        var previousStart = job.ScheduledStart;
        var from = job.Status;

        job.AssignedTechnicianId = tech.Id;
        job.AssignedByDispatcherId = dto.DispatcherId;
        job.AssignedAt = now;
        job.ScheduledStart = validation.ProposedStart;
        job.ScheduledEnd = validation.ProposedEnd;
        if (job.Status is JobStatus.Unassigned or JobStatus.OnHold or JobStatus.Dispatched or JobStatus.EnRoute)
            job.Status = JobStatus.Assigned;
        job.OnHoldReason = null;
        job.UpdatedAt = now;

        if (previousTech.HasValue && previousTech != tech.Id)
        {
            db.JobEvents.Add(new JobEvent { JobId = job.Id, EventType = JobEventType.Unassigned, FromStatus = from, ToStatus = JobStatus.Unassigned,
                TechnicianId = previousTech, DispatcherId = dto.DispatcherId, OccurredAt = now, Details = "Reassigned" });
            db.Reschedules.Add(new Reschedule { JobId = job.Id, FromTechnicianId = previousTech, ToTechnicianId = tech.Id, FromStart = previousStart,
                ToStart = job.ScheduledStart, ReasonCode = RescheduleReason.SlaRebalance, Comment = dto.Note ?? "Reassigned", DispatcherId = dto.DispatcherId, CreatedAt = now });
            job.RescheduleCount++;
        }
        db.JobEvents.Add(new JobEvent { JobId = job.Id, EventType = JobEventType.Assigned, FromStatus = from, ToStatus = job.Status, TechnicianId = tech.Id,
            DispatcherId = dto.DispatcherId, OccurredAt = now,
            Details = $"Assigned to {tech.FirstName} {tech.LastName} for {job.ScheduledStart:yyyy-MM-dd HH:mm}" + (dto.Force && !validation.CanAssign ? " (forced)" : "") });
        if (!string.IsNullOrWhiteSpace(dto.Note))
            db.JobNotes.Add(new JobNote { JobId = job.Id, AuthorType = NoteAuthorType.Dispatcher, AuthorName = await DispatcherName(dto.DispatcherId, ct),
                Body = dto.Note, IsInternal = true, CreatedAt = now });
        await db.SaveChangesAsync(ct);
        return new AssignmentResultDto(await jobs.GetDetailAsync(job.Id, ct), validation);
    }

    public async Task<JobDetailDto> UnassignAsync(int jobId, UnassignJobDto dto, CancellationToken ct)
    {
        var job = await LoadJobAsync(jobId, ct);
        if (job.AssignedTechnicianId == null) throw ApiException.Conflict("Job is not assigned.");
        if (job.Status is JobStatus.OnSite or JobStatus.Completed or JobStatus.Cancelled)
            throw ApiException.Conflict($"A job that is {job.Status} cannot be unassigned.");
        UnassignInternal(job, dto.DispatcherId, dto.Reason);
        await db.SaveChangesAsync(ct);
        return await jobs.GetDetailAsync(job.Id, ct);
    }

    private void UnassignInternal(Job job, int? dispatcherId, string? reason)
    {
        var now = clock.Now;
        db.JobEvents.Add(new JobEvent { JobId = job.Id, EventType = JobEventType.Unassigned, FromStatus = job.Status, ToStatus = JobStatus.Unassigned,
            TechnicianId = job.AssignedTechnicianId, DispatcherId = dispatcherId, OccurredAt = now, Details = reason });
        job.AssignedTechnicianId = null;
        job.AssignedByDispatcherId = null;
        job.AssignedAt = null;
        job.ScheduledStart = null;
        job.ScheduledEnd = null;
        job.Status = JobStatus.Unassigned;
        job.UpdatedAt = now;
    }

    // ------------------------------------------------------------------ reschedule
    public async Task<AssignmentResultDto> RescheduleAsync(int jobId, RescheduleJobDto dto, CancellationToken ct)
    {
        var job = await LoadJobAsync(jobId, ct);
        if (!job.IsOpen) throw ApiException.Conflict($"Job is {job.Status} and cannot be rescheduled.");
        if (job.Status == JobStatus.OnSite) throw ApiException.Conflict("Technician is on site; put the job on hold before rescheduling.");

        var now = clock.Now;
        var targetTechId = dto.TechnicianId ?? job.AssignedTechnicianId;
        var fromTech = job.AssignedTechnicianId;
        var fromStart = job.ScheduledStart ?? job.WindowStart;
        var delta = dto.NewScheduledStart - fromStart;

        if (dto.UpdateWindow)
        {
            var len = job.WindowEnd - job.WindowStart;
            job.WindowStart = dto.NewScheduledStart;
            job.WindowEnd = dto.NewScheduledStart + len;
            // Customer-requested moves re-baseline the SLA; operational moves keep the original commitment.
            if (dto.ReasonCode == RescheduleReason.CustomerRequest)
            {
                var newResponse = Max(job.ResponseDueAt, job.WindowEnd);
                job.ResolutionDueAt = job.ResolutionDueAt + (newResponse - job.ResponseDueAt);
                job.ResponseDueAt = newResponse;
            }
        }

        AssignmentValidationDto validation;
        if (targetTechId.HasValue)
        {
            var tech = await LoadTechAsync(targetTechId.Value, ct);
            (validation, _) = await BuildValidationAsync(job, tech, dto.NewScheduledStart, ct);
            if (!validation.CanAssign && !dto.Force)
                throw ApiException.Conflict("Reschedule has blocking issues. Resolve them or resend with force=true.", validation);
            job.AssignedTechnicianId = tech.Id;
            job.ScheduledStart = dto.NewScheduledStart;
            job.ScheduledEnd = dto.NewScheduledStart.AddMinutes(job.EstimatedDurationMinutes);
            if (fromTech != tech.Id) { job.AssignedAt = now; job.AssignedByDispatcherId = dto.DispatcherId; }
            if (job.Status is JobStatus.Dispatched or JobStatus.EnRoute or JobStatus.OnHold or JobStatus.Unassigned) job.Status = JobStatus.Assigned;
        }
        else
        {
            validation = new AssignmentValidationDto(true, dto.NewScheduledStart, dto.NewScheduledStart.AddMinutes(job.EstimatedDurationMinutes), []);
        }

        job.RescheduleCount++;
        job.UpdatedAt = now;
        job.OnHoldReason = null;
        db.Reschedules.Add(new Reschedule { JobId = job.Id, FromTechnicianId = fromTech, ToTechnicianId = job.AssignedTechnicianId, FromStart = fromStart,
            ToStart = dto.NewScheduledStart, ReasonCode = dto.ReasonCode, Comment = dto.Comment, DispatcherId = dto.DispatcherId, CreatedAt = now });
        db.JobEvents.Add(new JobEvent { JobId = job.Id, EventType = JobEventType.Rescheduled, TechnicianId = job.AssignedTechnicianId, DispatcherId = dto.DispatcherId,
            OccurredAt = now, Details = $"{fromStart:yyyy-MM-dd HH:mm} -> {dto.NewScheduledStart:yyyy-MM-dd HH:mm} ({dto.ReasonCode}, {(delta.TotalMinutes >= 0 ? "+" : "")}{delta.TotalMinutes:0} min)" });
        await db.SaveChangesAsync(ct);
        return new AssignmentResultDto(await jobs.GetDetailAsync(job.Id, ct), validation);
    }

    // ------------------------------------------------------------------ technician-level reschedule journeys
    /// <summary>Move every not-yet-started job on a technician's day (e.g. tech called in sick).</summary>
    public async Task<ReassignDayResultDto> ReassignDayAsync(int techId, ReassignDayDto dto, CancellationToken ct)
    {
        var tech = await LoadTechAsync(techId, ct);
        if (dto.Mode == ReassignMode.ToTechnician && dto.TargetTechnicianId is null)
            throw ApiException.BadRequest("TargetTechnicianId is required when Mode is ToTechnician.");
        if (dto.TargetTechnicianId == techId) throw ApiException.BadRequest("Target technician must be different.");

        var dayStart = dto.Date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var jobIds = await db.Jobs.Where(j => j.AssignedTechnicianId == techId && j.ScheduledStart >= dayStart && j.ScheduledStart < dayEnd &&
                                              (j.Status == JobStatus.Assigned || j.Status == JobStatus.Dispatched || j.Status == JobStatus.EnRoute || j.Status == JobStatus.OnHold))
            .OrderBy(j => j.Priority == JobPriority.Critical ? 0 : j.Priority == JobPriority.High ? 1 : j.Priority == JobPriority.Medium ? 2 : 3).ThenBy(j => j.ScheduledStart).Select(j => j.Id).ToListAsync(ct);

        var now = clock.Now;
        var results = new List<ReassignJobResultDto>();
        foreach (var id in jobIds)
        {
            var job = await LoadJobAsync(id, ct);
            var fromStart = job.ScheduledStart;
            int? newTech = null; DateTime? newStart = null; string? msg = null;

            if (dto.Mode == ReassignMode.AutoDistribute)
            {
                var best = (await RecommendAsync(id, 1, false, techId, false, ct)).FirstOrDefault();
                if (best?.EarliestStart != null) { newTech = best.TechnicianId; newStart = best.EarliestStart; }
                else msg = "No eligible technician with a free slot";
            }
            else if (dto.Mode == ReassignMode.ToTechnician)
            {
                var target = await LoadTechAsync(dto.TargetTechnicianId!.Value, ct);
                var (v, _) = await BuildValidationAsync(job, target, null, ct);
                if (v.CanAssign) { newTech = target.Id; newStart = v.ProposedStart; }
                else msg = string.Join("; ", v.Issues.Where(i => i.Severity == IssueSeverity.Blocking).Select(i => i.Message));
            }

            if (newTech.HasValue)
            {
                job.AssignedTechnicianId = newTech;
                job.ScheduledStart = newStart;
                job.ScheduledEnd = newStart!.Value.AddMinutes(job.EstimatedDurationMinutes);
                job.AssignedAt = now;
                job.AssignedByDispatcherId = dto.DispatcherId;
                job.Status = JobStatus.Assigned;
                job.RescheduleCount++;
                job.UpdatedAt = now;
                db.Reschedules.Add(new Reschedule { JobId = id, FromTechnicianId = techId, ToTechnicianId = newTech, FromStart = fromStart, ToStart = newStart,
                    ReasonCode = dto.ReasonCode, Comment = dto.Comment, DispatcherId = dto.DispatcherId, CreatedAt = now });
                db.JobEvents.Add(new JobEvent { JobId = id, EventType = JobEventType.Rescheduled, TechnicianId = newTech, DispatcherId = dto.DispatcherId,
                    OccurredAt = now, Details = $"Moved from {tech.FirstName} {tech.LastName} ({dto.ReasonCode})" });
                await db.SaveChangesAsync(ct); // persist so the next recommendation sees this booking
                var name = await db.Technicians.Where(t => t.Id == newTech).Select(t => t.FirstName + " " + t.LastName).SingleAsync(ct);
                results.Add(new(id, job.JobNumber, "Reassigned", newTech, name, newStart, null));
            }
            else
            {
                UnassignInternal(job, dto.DispatcherId, $"Technician unavailable ({dto.ReasonCode})");
                db.Reschedules.Add(new Reschedule { JobId = id, FromTechnicianId = techId, FromStart = fromStart, ReasonCode = dto.ReasonCode,
                    Comment = dto.Comment ?? msg, DispatcherId = dto.DispatcherId, CreatedAt = now });
                job.RescheduleCount++;
                await db.SaveChangesAsync(ct);
                results.Add(new(id, job.JobNumber, "Unassigned", null, null, null, msg));
            }
        }

        if (dto.MarkShiftAbsent)
        {
            var shifts = await db.Shifts.Where(s => s.TechnicianId == techId && s.ShiftDate == dto.Date).ToListAsync(ct);
            foreach (var s in shifts) { s.Status = ShiftStatus.Absent; s.Notes = dto.Comment ?? dto.ReasonCode.ToString(); }
            if (dto.Date == clock.Today) tech.FieldStatus = FieldStatus.Offline;
            await db.SaveChangesAsync(ct);
        }
        return new ReassignDayResultDto(techId, dto.Date, results.Count, results.Count(r => r.Outcome == "Reassigned"),
            results.Count(r => r.Outcome == "Unassigned"), results);
    }

    /// <summary>Change a technician's shift hours and optionally slide their not-started jobs by the same delta.</summary>
    public async Task<(ShiftDto Shift, IReadOnlyList<ReassignJobResultDto> Moved)> MoveShiftAsync(int shiftId, ShiftMoveDto dto, CancellationToken ct)
    {
        if (dto.NewEndTime <= dto.NewStartTime) throw ApiException.BadRequest("NewEndTime must be after NewStartTime.");
        var shift = await db.Shifts.Include(s => s.Technician).SingleOrDefaultAsync(s => s.Id == shiftId, ct) ?? throw ApiException.NotFound("Shift", shiftId);
        var delta = dto.NewStartTime.ToTimeSpan() - shift.StartTime.ToTimeSpan();
        shift.StartTime = dto.NewStartTime;
        shift.EndTime = dto.NewEndTime;
        var moved = new List<ReassignJobResultDto>();
        var now = clock.Now;
        if (dto.MoveJobs && delta != TimeSpan.Zero)
        {
            var dayStart = shift.ShiftDate.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var dayJobs = await db.Jobs.Where(j => j.AssignedTechnicianId == shift.TechnicianId && j.ScheduledStart >= dayStart && j.ScheduledStart < dayEnd &&
                                                   (j.Status == JobStatus.Assigned || j.Status == JobStatus.Dispatched)).ToListAsync(ct);
            foreach (var j in dayJobs)
            {
                var from = j.ScheduledStart!.Value;
                j.ScheduledStart = from + delta;
                j.ScheduledEnd = j.ScheduledStart.Value.AddMinutes(j.EstimatedDurationMinutes);
                j.RescheduleCount++;
                j.UpdatedAt = now;
                db.Reschedules.Add(new Reschedule { JobId = j.Id, FromTechnicianId = j.AssignedTechnicianId, ToTechnicianId = j.AssignedTechnicianId,
                    FromStart = from, ToStart = j.ScheduledStart, ReasonCode = dto.ReasonCode, Comment = "Shift moved", DispatcherId = dto.DispatcherId, CreatedAt = now });
                db.JobEvents.Add(new JobEvent { JobId = j.Id, EventType = JobEventType.Rescheduled, TechnicianId = j.AssignedTechnicianId, DispatcherId = dto.DispatcherId,
                    OccurredAt = now, Details = $"Shift moved {delta.TotalMinutes:+0;-0} min" });
                moved.Add(new(j.Id, j.JobNumber, "Moved", j.AssignedTechnicianId, $"{shift.Technician.FirstName} {shift.Technician.LastName}", j.ScheduledStart, null));
            }
        }
        await db.SaveChangesAsync(ct);
        return (new ShiftDto(shift.Id, shift.TechnicianId, $"{shift.Technician.FirstName} {shift.Technician.LastName}", shift.ShiftDate,
            shift.StartTime, shift.EndTime, shift.ShiftType, shift.Status, shift.Notes), moved);
    }

    private async Task<string> DispatcherName(int? id, CancellationToken ct) => id == null ? "Dispatcher"
        : await db.Dispatchers.Where(d => d.Id == id).Select(d => d.FirstName + " " + d.LastName).SingleOrDefaultAsync(ct) ?? "Dispatcher";
}
