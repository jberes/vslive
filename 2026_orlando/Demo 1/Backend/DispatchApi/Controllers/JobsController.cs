using DispatchApi.Data;
using DispatchApi.Dtos;
using DispatchApi.Infrastructure;
using DispatchApi.Models;
using DispatchApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Controllers;

[ApiController, Route("api/jobs"), Tags("Jobs")]
public class JobsController(JobService jobs, SchedulingService scheduling, DispatchDbContext db) : ControllerBase
{
    /// <summary>
    /// Paged, filterable job list. Every row carries a computed SLA status.
    /// Examples: ?date=2026-11-11&amp;status=Unassigned&amp;priority=Critical&amp;priority=High,
    /// ?openOnly=true&amp;slaState=AtRisk&amp;slaState=Breached, ?sort=-priority,scheduledStart.
    /// Sort fields: any JobListItem property (priority sorts by severity).
    /// </summary>
    [HttpGet]
    public Task<PagedResult<JobListItemDto>> List([FromQuery] JobQuery q, CancellationToken ct) => jobs.ListAsync(q, ct);

    /// <summary>Full job detail with timeline events, notes, parts and reschedule history.</summary>
    [HttpGet("{id:int}")]
    public Task<JobDetailDto> Get(int id, CancellationToken ct) => jobs.GetDetailAsync(id, ct);

    /// <summary>Look up a job by its work-order number (e.g. WO-261111-012345).</summary>
    [HttpGet("by-number/{jobNumber}")]
    public async Task<JobDetailDto> GetByNumber(string jobNumber, CancellationToken ct)
    {
        var id = await db.Jobs.Where(j => j.JobNumber == jobNumber).Select(j => (int?)j.Id).SingleOrDefaultAsync(ct)
                 ?? throw ApiException.NotFound("Job", jobNumber);
        return await jobs.GetDetailAsync(id, ct);
    }

    /// <summary>Create a job. SLA policy and due times are derived from the customer tier and priority.</summary>
    [HttpPost]
    public async Task<ActionResult<JobDetailDto>> Create(JobCreateDto dto, CancellationToken ct)
    {
        var created = await jobs.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Job.Id }, created);
    }

    /// <summary>Partial update of editable fields. Changing priority re-links the SLA policy.</summary>
    [HttpPatch("{id:int}")]
    public Task<JobDetailDto> Update(int id, JobUpdateDto dto, CancellationToken ct) => jobs.UpdateAsync(id, dto, ct);

    /// <summary>Cancels the job (jobs are never hard-deleted).</summary>
    [HttpDelete("{id:int}")]
    public Task<JobDetailDto> Cancel(int id, [FromQuery] string? reason, [FromQuery] int? dispatcherId, CancellationToken ct) =>
        jobs.ChangeStatusAsync(id, new StatusChangeDto { Status = JobStatus.Cancelled, Note = reason ?? "Cancelled", DispatcherId = dispatcherId }, ct);

    // ---------------------------------------------------------------- assign-a-job journey
    /// <summary>Ranked technicians for this job: skill, proficiency, shift, free slot, travel, workload and SLA fit.</summary>
    /// <param name="limit">Max results (default 10).</param>
    /// <param name="includeIneligible">Include technicians with blocking issues (shown with their issues).</param>
    /// <param name="crossRegion">Also consider technicians from other regions.</param>
    [HttpGet("{id:int}/recommendations")]
    public Task<IReadOnlyList<TechnicianRecommendationDto>> Recommendations(int id, [FromQuery] int limit = 10,
        [FromQuery] bool includeIneligible = false, [FromQuery] bool crossRegion = false, CancellationToken ct = default) =>
        scheduling.RecommendAsync(id, limit, includeIneligible, null, crossRegion, ct);

    /// <summary>Dry run: what would happen if this technician took the job (at an optional start time).</summary>
    [HttpGet("{id:int}/validate-assignment")]
    public Task<AssignmentValidationDto> ValidateAssignment(int id, [FromQuery] int technicianId, [FromQuery] DateTime? scheduledStart, CancellationToken ct) =>
        scheduling.ValidateAsync(id, technicianId, scheduledStart, ct);

    /// <summary>Assign (or reassign) a job. Returns 409 with validation issues unless force=true.</summary>
    [HttpPost("{id:int}/assign")]
    public Task<AssignmentResultDto> Assign(int id, AssignJobDto dto, CancellationToken ct) => scheduling.AssignAsync(id, dto, ct);

    [HttpPost("{id:int}/unassign")]
    public Task<JobDetailDto> Unassign(int id, UnassignJobDto dto, CancellationToken ct) => scheduling.UnassignAsync(id, dto, ct);

    /// <summary>Move a job to a new time and/or technician. Records reschedule history.</summary>
    [HttpPost("{id:int}/reschedule")]
    public Task<AssignmentResultDto> Reschedule(int id, RescheduleJobDto dto, CancellationToken ct) => scheduling.RescheduleAsync(id, dto, ct);

    /// <summary>Workflow status change (Dispatched, EnRoute, OnSite, OnHold, Completed, Cancelled...). Invalid transitions return 409.</summary>
    [HttpPost("{id:int}/status")]
    public Task<JobDetailDto> ChangeStatus(int id, StatusChangeDto dto, CancellationToken ct) => jobs.ChangeStatusAsync(id, dto, ct);

    // ---------------------------------------------------------------- sub-resources
    [HttpGet("{id:int}/events")]
    public async Task<IReadOnlyList<JobEventDto>> Events(int id, CancellationToken ct) => (await jobs.GetDetailAsync(id, ct)).Events;

    [HttpGet("{id:int}/notes")]
    public async Task<IReadOnlyList<JobNoteDto>> Notes(int id, CancellationToken ct) => (await jobs.GetDetailAsync(id, ct)).Notes;

    [HttpPost("{id:int}/notes")]
    public Task<JobNoteDto> AddNote(int id, NoteCreateDto dto, CancellationToken ct) => jobs.AddNoteAsync(id, dto, ct);

    [HttpGet("{id:int}/parts")]
    public async Task<IReadOnlyList<JobPartDto>> Parts(int id, CancellationToken ct) => (await jobs.GetDetailAsync(id, ct)).Parts;

    [HttpPost("{id:int}/parts")]
    public Task<JobPartDto> AddPart(int id, JobPartCreateDto dto, CancellationToken ct) => jobs.AddPartAsync(id, dto, ct);

    [HttpDelete("{id:int}/parts/{jobPartId:int}")]
    public async Task<IActionResult> RemovePart(int id, int jobPartId, CancellationToken ct)
    {
        await jobs.RemovePartAsync(id, jobPartId, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/reschedules")]
    public async Task<IReadOnlyList<RescheduleDto>> Reschedules(int id, CancellationToken ct) => (await jobs.GetDetailAsync(id, ct)).Reschedules;
}

[ApiController, Route("api/dispatch"), Tags("Dispatch board")]
public class DispatchController(AnalyticsService analytics, SchedulingService scheduling) : ControllerBase
{
    /// <summary>
    /// Gantt-style board: every technician on shift (with shift bounds, utilization and that day's jobs) plus the unassigned queue
    /// ordered by SLA urgency. Filter by region/depot/skill to keep it manageable.
    /// </summary>
    [HttpGet("board")]
    public Task<DispatchBoardDto> Board([FromQuery] DateOnly? date, [FromQuery] int? regionId, [FromQuery] int? depotId, [FromQuery] int? skillId,
        [FromQuery] bool includeOffShift = false, CancellationToken ct = default) =>
        analytics.BoardAsync(date, regionId, depotId, skillId, includeOffShift, ct);

    /// <summary>
    /// Reschedule-a-technician journey: move all not-yet-started jobs for a technician's day
    /// (auto-distribute to best-fit colleagues, hand to one technician, or return to the queue).
    /// </summary>
    [HttpPost("technicians/{technicianId:int}/reassign-day")]
    public Task<ReassignDayResultDto> ReassignDay(int technicianId, ReassignDayDto dto, CancellationToken ct) =>
        scheduling.ReassignDayAsync(technicianId, dto, ct);
}

[ApiController, Route("api/sla"), Tags("SLA")]
public class SlaController(AnalyticsService analytics) : ControllerBase
{
    /// <summary>Today's SLA risk picture: counts by state, compliance, penalty exposure, breakdowns and due-by-hour.</summary>
    [HttpGet("summary")]
    public Task<SlaSummaryDto> Summary([FromQuery] DateOnly? date, [FromQuery] int? regionId, CancellationToken ct) =>
        analytics.SlaSummaryAsync(date, regionId, ct);

    /// <summary>Jobs at risk or breached, most urgent first (includes carried-over overdue jobs).</summary>
    /// <param name="state">Defaults to AtRisk + Breached.</param>
    [HttpGet("at-risk")]
    public Task<IReadOnlyList<JobListItemDto>> AtRisk([FromQuery] DateOnly? date, [FromQuery] int? regionId, [FromQuery] List<SlaState>? state,
        [FromQuery] JobPriority? priority, [FromQuery] int limit = 500, CancellationToken ct = default) =>
        analytics.SlaAtRiskAsync(date, regionId, state, priority, limit, ct);

    /// <summary>Daily response/resolution compliance for completed jobs over the last N days.</summary>
    [HttpGet("trend")]
    public Task<IReadOnlyList<SlaTrendPointDto>> Trend([FromQuery] int days = 30, [FromQuery] int? regionId = null, CancellationToken ct = default) =>
        analytics.SlaTrendAsync(days, regionId, ct);
}

[ApiController, Route("api/dashboard"), Tags("Dashboard")]
public class DashboardController(AnalyticsService analytics) : ControllerBase
{
    /// <summary>Headline KPIs for a day (default today).</summary>
    [HttpGet("kpis")]
    public Task<KpiDto> Kpis([FromQuery] DateOnly? date, [FromQuery] int? regionId, CancellationToken ct) => analytics.KpisAsync(date, regionId, ct);

    /// <summary>Scheduled / completed / unassigned jobs per hour (05:00-20:00).</summary>
    [HttpGet("jobs-by-hour")]
    public Task<IReadOnlyList<HourBucketDto>> JobsByHour([FromQuery] DateOnly? date, [FromQuery] int? regionId, CancellationToken ct) =>
        analytics.JobsByHourAsync(date, regionId, ct);

    [HttpGet("jobs-by-status")]
    public Task<IReadOnlyList<StatusCountDto>> JobsByStatus([FromQuery] DateOnly? date, [FromQuery] int? regionId, CancellationToken ct) =>
        analytics.StatusBreakdownAsync(date, regionId, ct);

    [HttpGet("jobs-by-category")]
    public Task<IReadOnlyList<StatusCountDto>> JobsByCategory([FromQuery] DateOnly? date, [FromQuery] int? regionId, CancellationToken ct) =>
        analytics.JobsByCategoryAsync(date, regionId, ct);

    [HttpGet("tech-utilization")]
    public Task<IReadOnlyList<TechUtilizationDto>> TechUtilization([FromQuery] DateOnly? date, [FromQuery] int? regionId, CancellationToken ct) =>
        analytics.TechUtilizationAsync(date, regionId, ct);
}

[ApiController, Route("api/reschedules"), Tags("Jobs")]
public class ReschedulesController(DispatchDbContext db, IClock clock) : ControllerBase
{
    /// <summary>Reschedule audit trail (default: last 7 days).</summary>
    [HttpGet]
    public async Task<PagedResult<RescheduleDto>> List([FromQuery] PageQuery q, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? technicianId, [FromQuery] int? dispatcherId, [FromQuery] RescheduleReason? reason, [FromQuery] int? regionId, CancellationToken ct)
    {
        var f = (from ?? clock.Today.AddDays(-6)).ToDateTime(TimeOnly.MinValue);
        var e = (to ?? clock.Today).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var query = db.Reschedules.AsNoTracking().Where(r => r.CreatedAt >= f && r.CreatedAt < e);
        if (technicianId.HasValue) query = query.Where(r => r.FromTechnicianId == technicianId || r.ToTechnicianId == technicianId);
        if (dispatcherId.HasValue) query = query.Where(r => r.DispatcherId == dispatcherId);
        if (reason.HasValue) query = query.Where(r => r.ReasonCode == reason);
        if (regionId.HasValue) query = query.Where(r => r.Job.RegionId == regionId);
        var total = await query.CountAsync(ct);
        var items = await JobService.RescheduleProjection(query.ApplySort(q.Sort, "-CreatedAt").Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize))
            .ToListAsync(ct);
        return new PagedResult<RescheduleDto>(items, total, q.SafePage, q.SafePageSize);
    }
}
