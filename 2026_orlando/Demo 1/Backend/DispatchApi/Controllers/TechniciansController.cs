using System.Linq.Expressions;
using DispatchApi.Data;
using DispatchApi.Dtos;
using DispatchApi.Infrastructure;
using DispatchApi.Models;
using DispatchApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Controllers;

public class TechnicianQuery : PageQuery
{
    public int? RegionId { get; set; }
    public int? DepotId { get; set; }
    public int? SkillId { get; set; }
    public List<EmploymentStatus>? EmploymentStatus { get; set; }
    public List<FieldStatus>? FieldStatus { get; set; }
    public List<TechnicianLevel>? Level { get; set; }
    /// <summary>Only technicians with a working shift on this date.</summary>
    public DateOnly? OnShiftDate { get; set; }
}

[ApiController, Route("api/technicians"), Tags("Technicians")]
public class TechniciansController(DispatchDbContext db, IClock clock, AnalyticsService analytics) : ControllerBase
{
    /// <summary>Paged technician list. Sort by any property, e.g. "lastName", "-rating", "depot.name".</summary>
    [HttpGet]
    public Task<PagedResult<TechnicianListItemDto>> List([FromQuery] TechnicianQuery q, CancellationToken ct)
    {
        var query = db.Technicians.AsNoTracking().AsQueryable();
        if (q.RegionId.HasValue) query = query.Where(t => t.Depot.RegionId == q.RegionId);
        if (q.DepotId.HasValue) query = query.Where(t => t.DepotId == q.DepotId);
        if (q.SkillId.HasValue) query = query.Where(t => t.Skills.Any(s => s.SkillId == q.SkillId));
        if (q.EmploymentStatus is { Count: > 0 }) query = query.Where(t => q.EmploymentStatus.Contains(t.EmploymentStatus));
        if (q.FieldStatus is { Count: > 0 }) query = query.Where(t => q.FieldStatus.Contains(t.FieldStatus));
        if (q.Level is { Count: > 0 }) query = query.Where(t => q.Level.Contains(t.Level));
        if (q.OnShiftDate.HasValue)
            query = query.Where(t => t.Shifts.Any(s => s.ShiftDate == q.OnShiftDate && s.Status != ShiftStatus.Absent && s.Status != ShiftStatus.Cancelled));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(t => EF.Functions.Like((t.FirstName + " " + t.LastName), "%" + q.Search + "%") || EF.Functions.Like(t.EmployeeNumber, "%" + q.Search + "%") || EF.Functions.Like(t.Email, "%" + q.Search + "%"));
        return query.ToPagedAsync(q, t => new TechnicianListItemDto(t.Id, t.EmployeeNumber, t.FirstName, t.LastName, t.FirstName + " " + t.LastName,
            t.Email, t.Phone, t.DepotId, t.Depot.Name, t.Depot.RegionId, t.Depot.Region.Name, t.Level, t.EmploymentStatus, t.FieldStatus, t.Rating,
            t.MaxJobsPerDay, t.Vehicle == null ? null : t.Vehicle.PlateNumber,
            string.Join(", ", t.Skills.OrderBy(s => s.Skill.Code).Select(s => s.Skill.Code))), "LastName,FirstName", ct);
    }

    /// <summary>Technician profile with skills and today's workload.</summary>
    [HttpGet("{id:int}")]
    public async Task<TechnicianDetailDto> Get(int id, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var t = await db.Technicians.AsNoTracking().Include(x => x.Depot).ThenInclude(d => d.Region).Include(x => x.Vehicle)
            .Include(x => x.Skills).ThenInclude(s => s.Skill).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("Technician", id);
        var d = date ?? clock.Today;
        return new TechnicianDetailDto(t.Id, t.EmployeeNumber, t.FirstName, t.LastName, $"{t.FirstName} {t.LastName}", t.Email, t.Phone,
            t.DepotId, t.Depot.Name, t.Depot.RegionId, t.Depot.Region.Name, t.VehicleId, t.Vehicle?.PlateNumber,
            t.Vehicle == null ? null : $"{t.Vehicle.Year} {t.Vehicle.Make} {t.Vehicle.Model}", t.Level, t.EmploymentStatus, t.FieldStatus, t.HireDate,
            t.HourlyRate, t.MaxJobsPerDay, t.Rating, t.HomeLatitude, t.HomeLongitude, t.CurrentLatitude, t.CurrentLongitude, t.LastLocationAt,
            t.Skills.OrderBy(s => s.Skill.Name).Select(s => new TechnicianSkillDto(s.SkillId, s.Skill.Code, s.Skill.Name, s.Proficiency, s.CertifiedUntil,
                s.CertifiedUntil.HasValue && s.CertifiedUntil < d)).ToList(),
            await DayStats(id, d, ct));
    }

    private async Task<TechnicianDayStatsDto> DayStats(int id, DateOnly d, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking().Where(s => s.TechnicianId == id && s.ShiftDate == d && s.Status != ShiftStatus.Absent && s.Status != ShiftStatus.Cancelled)
            .FirstOrDefaultAsync(ct);
        var start = d.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var jobs = await db.Jobs.AsNoTracking().Where(j => j.AssignedTechnicianId == id && j.ScheduledStart >= start && j.ScheduledStart < end && j.Status != JobStatus.Cancelled)
            .Select(j => new { j.Status, j.EstimatedDurationMinutes }).ToListAsync(ct);
        var shiftMin = shift == null ? 0 : (int)(shift.EndTime - shift.StartTime).TotalMinutes;
        var booked = jobs.Sum(j => j.EstimatedDurationMinutes);
        return new TechnicianDayStatsDto(d, shift != null, shift?.StartTime, shift?.EndTime, jobs.Count, jobs.Count(j => j.Status == JobStatus.Completed),
            jobs.Count(j => j.Status != JobStatus.Completed), booked, Math.Max(0, shiftMin - booked), shiftMin == 0 ? 0 : Math.Round(booked * 100.0 / shiftMin, 1));
    }

    [HttpPost]
    public async Task<ActionResult<TechnicianDetailDto>> Create(TechnicianUpsertDto dto, CancellationToken ct)
    {
        var depot = await db.Depots.FindAsync([dto.DepotId], ct) ?? throw ApiException.BadRequest($"Depot {dto.DepotId} does not exist.");
        if (await db.Technicians.AnyAsync(t => t.EmployeeNumber == dto.EmployeeNumber || t.Email == dto.Email, ct))
            throw ApiException.Conflict("A technician with that employee number or email already exists.");
        var t = new Technician { HomeLatitude = depot.Latitude, HomeLongitude = depot.Longitude };
        Apply(t, dto);
        t.HireDate = dto.HireDate ?? clock.Today;
        db.Technicians.Add(t);
        await db.SaveChangesAsync(ct);
        await ReplaceSkills(t.Id, dto.Skills, ct);
        return CreatedAtAction(nameof(Get), new { id = t.Id }, await Get(t.Id, null, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<TechnicianDetailDto> Update(int id, TechnicianUpsertDto dto, CancellationToken ct)
    {
        var t = await db.Technicians.FindAsync([id], ct) ?? throw ApiException.NotFound("Technician", id);
        if (!await db.Depots.AnyAsync(d => d.Id == dto.DepotId, ct)) throw ApiException.BadRequest($"Depot {dto.DepotId} does not exist.");
        if (await db.Technicians.AnyAsync(x => x.Id != id && (x.EmployeeNumber == dto.EmployeeNumber || x.Email == dto.Email), ct))
            throw ApiException.Conflict("Another technician has that employee number or email.");
        Apply(t, dto);
        if (dto.HireDate.HasValue) t.HireDate = dto.HireDate.Value;
        await db.SaveChangesAsync(ct);
        if (dto.Skills != null) await ReplaceSkills(id, dto.Skills, ct);
        return await Get(id, null, ct);
    }

    private static void Apply(Technician t, TechnicianUpsertDto d)
    {
        t.EmployeeNumber = d.EmployeeNumber; t.FirstName = d.FirstName; t.LastName = d.LastName; t.Email = d.Email; t.Phone = d.Phone;
        t.DepotId = d.DepotId; t.VehicleId = d.VehicleId; t.Level = d.Level; t.EmploymentStatus = d.EmploymentStatus; t.FieldStatus = d.FieldStatus;
        t.HourlyRate = d.HourlyRate; t.MaxJobsPerDay = d.MaxJobsPerDay; t.Rating = d.Rating;
        if (d.HomeLatitude != 0 || d.HomeLongitude != 0) { t.HomeLatitude = d.HomeLatitude; t.HomeLongitude = d.HomeLongitude; }
    }

    /// <summary>Replace a technician's skill set.</summary>
    [HttpPut("{id:int}/skills")]
    public async Task<TechnicianDetailDto> PutSkills(int id, List<TechnicianSkillInput> skills, CancellationToken ct)
    {
        if (!await db.Technicians.AnyAsync(t => t.Id == id, ct)) throw ApiException.NotFound("Technician", id);
        await ReplaceSkills(id, skills, ct);
        return await Get(id, null, ct);
    }

    private async Task ReplaceSkills(int id, List<TechnicianSkillInput>? skills, CancellationToken ct)
    {
        if (skills == null) return;
        var valid = await db.Skills.Select(s => s.Id).ToListAsync(ct);
        var bad = skills.Where(s => !valid.Contains(s.SkillId)).Select(s => s.SkillId).ToList();
        if (bad.Count > 0) throw ApiException.BadRequest($"Unknown skill id(s): {string.Join(", ", bad)}");
        await db.TechnicianSkills.Where(s => s.TechnicianId == id).ExecuteDeleteAsync(ct);
        db.TechnicianSkills.AddRange(skills.DistinctBy(s => s.SkillId).Select(s => new TechnicianSkill
            { TechnicianId = id, SkillId = s.SkillId, Proficiency = s.Proficiency, CertifiedUntil = s.CertifiedUntil }));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Update live field status / GPS position (e.g. from the mobile app).</summary>
    [HttpPatch("{id:int}/field-status")]
    public async Task<TechnicianDetailDto> SetFieldStatus(int id, FieldStatusUpdateDto dto, CancellationToken ct)
    {
        var t = await db.Technicians.FindAsync([id], ct) ?? throw ApiException.NotFound("Technician", id);
        t.FieldStatus = dto.FieldStatus;
        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            t.CurrentLatitude = dto.Latitude; t.CurrentLongitude = dto.Longitude; t.LastLocationAt = clock.Now;
        }
        await db.SaveChangesAsync(ct);
        return await Get(id, null, ct);
    }

    /// <summary>Soft delete: marks the technician Inactive. Fails if they still have open jobs.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var t = await db.Technicians.FindAsync([id], ct) ?? throw ApiException.NotFound("Technician", id);
        var open = await db.Jobs.CountAsync(j => j.AssignedTechnicianId == id && JobService.OpenStatuses.Contains(j.Status), ct);
        if (open > 0) throw ApiException.Conflict($"Technician has {open} open job(s). Reassign them first (POST /api/dispatch/technicians/{id}/reassign-day).");
        t.EmploymentStatus = EmploymentStatus.Inactive;
        t.FieldStatus = FieldStatus.Offline;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Shifts, time off and jobs for a technician across a date range (default: today + 6 days).</summary>
    [HttpGet("{id:int}/schedule")]
    public async Task<TechnicianScheduleDto> Schedule(int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var t = await db.Technicians.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("Technician", id);
        var f = from ?? clock.Today;
        var e = to ?? f.AddDays(6);
        if (e < f) throw ApiException.BadRequest("'to' must be on or after 'from'.");
        if (e.DayNumber - f.DayNumber > 62) throw ApiException.BadRequest("Range is limited to 62 days.");
        var now = clock.Now;
        var shifts = await db.Shifts.AsNoTracking().Where(s => s.TechnicianId == id && s.ShiftDate >= f && s.ShiftDate <= e).OrderBy(s => s.ShiftDate)
            .Select(s => new ShiftDto(s.Id, s.TechnicianId, t.FirstName + " " + t.LastName, s.ShiftDate, s.StartTime, s.EndTime, s.ShiftType, s.Status, s.Notes))
            .ToListAsync(ct);
        var offs = await db.TimeOffRequests.AsNoTracking().Where(x => x.TechnicianId == id && x.StartDate <= e && x.EndDate >= f).OrderBy(x => x.StartDate)
            .Select(x => new TimeOffDto(x.Id, x.TechnicianId, t.FirstName + " " + t.LastName, x.StartDate, x.EndDate, x.Type, x.Status, x.Reason, x.RequestedAt))
            .ToListAsync(ct);
        var s0 = f.ToDateTime(TimeOnly.MinValue);
        var s1 = e.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var jobs = (await db.Jobs.AsNoTracking().Where(j => j.AssignedTechnicianId == id && j.ScheduledStart >= s0 && j.ScheduledStart < s1)
            .OrderBy(j => j.ScheduledStart).Select(JobProjections.ToRow).ToListAsync(ct)).Select(r => r.ToDto(now)).ToList();
        return new TechnicianScheduleDto(id, $"{t.FirstName} {t.LastName}", f, e, shifts, offs, jobs);
    }

    /// <summary>Utilization for every technician on shift on a date.</summary>
    [HttpGet("utilization")]
    public Task<IReadOnlyList<TechUtilizationDto>> Utilization([FromQuery] DateOnly? date, [FromQuery] int? regionId, CancellationToken ct) =>
        analytics.TechUtilizationAsync(date, regionId, ct);
}

[ApiController, Route("api/shifts"), Tags("Shifts & time off")]
public class ShiftsController(DispatchDbContext db, IClock clock, SchedulingService scheduling) : ControllerBase
{
    private IQueryable<ShiftDto> Query(Expression<Func<Shift, bool>> where) => db.Shifts.AsNoTracking().Where(where).Select(s => new ShiftDto(s.Id, s.TechnicianId,
        s.Technician.FirstName + " " + s.Technician.LastName, s.ShiftDate, s.StartTime, s.EndTime, s.ShiftType, s.Status, s.Notes));

    /// <summary>Shifts by date range / technician / region. Defaults to today.</summary>
    [HttpGet]
    public async Task<List<ShiftDto>> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? technicianId,
        [FromQuery] int? regionId, [FromQuery] ShiftStatus? status, CancellationToken ct)
    {
        var f = from ?? clock.Today;
        var e = to ?? f;
        var q = db.Shifts.AsNoTracking().Where(s => s.ShiftDate >= f && s.ShiftDate <= e);
        if (technicianId.HasValue) q = q.Where(s => s.TechnicianId == technicianId);
        if (regionId.HasValue) q = q.Where(s => s.Technician.Depot.RegionId == regionId);
        if (status.HasValue) q = q.Where(s => s.Status == status);
        return await q.OrderBy(s => s.ShiftDate).ThenBy(s => s.StartTime).ThenBy(s => s.Technician.LastName)
            .Select(s => new ShiftDto(s.Id, s.TechnicianId, s.Technician.FirstName + " " + s.Technician.LastName, s.ShiftDate, s.StartTime, s.EndTime,
                s.ShiftType, s.Status, s.Notes)).Take(5000).ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ShiftDto> Get(int id, CancellationToken ct) =>
        await Query(s => s.Id == id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Shift", id);

    [HttpPost]
    public async Task<ActionResult<ShiftDto>> Create(ShiftUpsertDto dto, CancellationToken ct)
    {
        await ValidateAsync(dto, null, ct);
        var s = new Shift { TechnicianId = dto.TechnicianId, ShiftDate = dto.ShiftDate, StartTime = dto.StartTime, EndTime = dto.EndTime,
            ShiftType = dto.ShiftType, Status = dto.Status, Notes = dto.Notes };
        db.Shifts.Add(s);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = s.Id }, await Get(s.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ShiftDto> Update(int id, ShiftUpsertDto dto, CancellationToken ct)
    {
        var s = await db.Shifts.FindAsync([id], ct) ?? throw ApiException.NotFound("Shift", id);
        await ValidateAsync(dto, id, ct);
        s.TechnicianId = dto.TechnicianId; s.ShiftDate = dto.ShiftDate; s.StartTime = dto.StartTime; s.EndTime = dto.EndTime;
        s.ShiftType = dto.ShiftType; s.Status = dto.Status; s.Notes = dto.Notes;
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    /// <summary>Reschedule-a-technician journey: change shift hours and slide that day's jobs by the same amount.</summary>
    [HttpPost("{id:int}/move")]
    public async Task<object> Move(int id, ShiftMoveDto dto, CancellationToken ct)
    {
        var (shift, moved) = await scheduling.MoveShiftAsync(id, dto, ct);
        return new { shift, movedJobs = moved };
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var s = await db.Shifts.FindAsync([id], ct) ?? throw ApiException.NotFound("Shift", id);
        db.Shifts.Remove(s);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ValidateAsync(ShiftUpsertDto dto, int? id, CancellationToken ct)
    {
        if (dto.EndTime <= dto.StartTime) throw ApiException.BadRequest("EndTime must be after StartTime.");
        if (!await db.Technicians.AnyAsync(t => t.Id == dto.TechnicianId, ct)) throw ApiException.BadRequest($"Technician {dto.TechnicianId} does not exist.");
        if (await db.Shifts.AnyAsync(s => s.Id != id && s.TechnicianId == dto.TechnicianId && s.ShiftDate == dto.ShiftDate &&
                                          s.Status != ShiftStatus.Cancelled && s.StartTime < dto.EndTime && s.EndTime > dto.StartTime, ct))
            throw ApiException.Conflict("Overlaps an existing shift for this technician.");
    }
}

[ApiController, Route("api/time-off"), Tags("Shifts & time off")]
public class TimeOffController(DispatchDbContext db, IClock clock) : ControllerBase
{
    private IQueryable<TimeOffDto> Query(Expression<Func<TimeOffRequest, bool>> where) => db.TimeOffRequests.AsNoTracking().Where(where).Select(x => new TimeOffDto(x.Id, x.TechnicianId,
        x.Technician.FirstName + " " + x.Technician.LastName, x.StartDate, x.EndDate, x.Type, x.Status, x.Reason, x.RequestedAt));

    /// <summary>Time-off requests overlapping a date range.</summary>
    [HttpGet]
    public Task<List<TimeOffDto>> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? technicianId,
        [FromQuery] TimeOffStatus? status, [FromQuery] int? regionId, CancellationToken ct)
    {
        var f = from ?? clock.Today.AddDays(-30);
        var e = to ?? clock.Today.AddDays(60);
        var q = db.TimeOffRequests.AsNoTracking().Where(x => x.StartDate <= e && x.EndDate >= f);
        if (technicianId.HasValue) q = q.Where(x => x.TechnicianId == technicianId);
        if (status.HasValue) q = q.Where(x => x.Status == status);
        if (regionId.HasValue) q = q.Where(x => x.Technician.Depot.RegionId == regionId);
        return q.OrderBy(x => x.StartDate).Select(x => new TimeOffDto(x.Id, x.TechnicianId, x.Technician.FirstName + " " + x.Technician.LastName,
            x.StartDate, x.EndDate, x.Type, x.Status, x.Reason, x.RequestedAt)).ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<TimeOffDto> Get(int id, CancellationToken ct) =>
        await Query(x => x.Id == id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Time off request", id);

    [HttpPost]
    public async Task<ActionResult<TimeOffDto>> Create(TimeOffUpsertDto dto, CancellationToken ct)
    {
        if (dto.EndDate < dto.StartDate) throw ApiException.BadRequest("EndDate must be on or after StartDate.");
        if (!await db.Technicians.AnyAsync(t => t.Id == dto.TechnicianId, ct)) throw ApiException.BadRequest($"Technician {dto.TechnicianId} does not exist.");
        var x = new TimeOffRequest { TechnicianId = dto.TechnicianId, StartDate = dto.StartDate, EndDate = dto.EndDate, Type = dto.Type,
            Status = dto.Status, Reason = dto.Reason, RequestedAt = clock.Now };
        db.TimeOffRequests.Add(x);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = x.Id }, await Get(x.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<TimeOffDto> Update(int id, TimeOffUpsertDto dto, CancellationToken ct)
    {
        var x = await db.TimeOffRequests.FindAsync([id], ct) ?? throw ApiException.NotFound("Time off request", id);
        if (dto.EndDate < dto.StartDate) throw ApiException.BadRequest("EndDate must be on or after StartDate.");
        x.TechnicianId = dto.TechnicianId; x.StartDate = dto.StartDate; x.EndDate = dto.EndDate; x.Type = dto.Type; x.Status = dto.Status; x.Reason = dto.Reason;
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    /// <summary>Approve a request. Response lists jobs the technician holds in that period that now need reassignment.</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<object> Approve(int id, CancellationToken ct) => await Decide(id, TimeOffStatus.Approved, ct);

    [HttpPost("{id:int}/deny")]
    public async Task<object> Deny(int id, CancellationToken ct) => await Decide(id, TimeOffStatus.Denied, ct);

    private async Task<object> Decide(int id, TimeOffStatus status, CancellationToken ct)
    {
        var x = await db.TimeOffRequests.FindAsync([id], ct) ?? throw ApiException.NotFound("Time off request", id);
        x.Status = status;
        await db.SaveChangesAsync(ct);
        var s0 = x.StartDate.ToDateTime(TimeOnly.MinValue);
        var s1 = x.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var conflicts = status == TimeOffStatus.Approved
            ? await db.Jobs.AsNoTracking().Where(j => j.AssignedTechnicianId == x.TechnicianId && j.ScheduledStart >= s0 && j.ScheduledStart < s1 &&
                                                      JobService.OpenStatuses.Contains(j.Status))
                .Select(j => new { j.Id, j.JobNumber, j.ScheduledStart, j.Status }).ToListAsync(ct)
            : [];
        return new { request = await Get(id, ct), conflictingJobs = conflicts };
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var x = await db.TimeOffRequests.FindAsync([id], ct) ?? throw ApiException.NotFound("Time off request", id);
        db.TimeOffRequests.Remove(x);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
