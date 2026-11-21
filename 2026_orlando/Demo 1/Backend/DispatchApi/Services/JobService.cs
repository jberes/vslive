using DispatchApi.Data;
using DispatchApi.Dtos;
using DispatchApi.Infrastructure;
using DispatchApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Services;

/// <summary>Job queries, CRUD, notes, parts and status workflow.</summary>
public class JobService(DispatchDbContext db, IClock clock)
{
    public static readonly JobStatus[] OpenStatuses =
        [JobStatus.Unassigned, JobStatus.Assigned, JobStatus.Dispatched, JobStatus.EnRoute, JobStatus.OnSite, JobStatus.OnHold];

    private static readonly Dictionary<JobStatus, JobStatus[]> Transitions = new()
    {
        [JobStatus.Unassigned] = [JobStatus.Cancelled, JobStatus.OnHold],
        [JobStatus.Assigned] = [JobStatus.Dispatched, JobStatus.EnRoute, JobStatus.OnHold, JobStatus.Cancelled],
        [JobStatus.Dispatched] = [JobStatus.Assigned, JobStatus.EnRoute, JobStatus.OnHold, JobStatus.Cancelled],
        [JobStatus.EnRoute] = [JobStatus.Dispatched, JobStatus.OnSite, JobStatus.OnHold, JobStatus.Cancelled],
        [JobStatus.OnSite] = [JobStatus.Completed, JobStatus.OnHold],
        [JobStatus.OnHold] = [JobStatus.Unassigned, JobStatus.Assigned, JobStatus.Dispatched, JobStatus.EnRoute, JobStatus.OnSite, JobStatus.Cancelled],
        [JobStatus.Completed] = [],
        [JobStatus.Cancelled] = [],
    };

    public IQueryable<Job> Filter(JobQuery q)
    {
        var query = db.Jobs.AsNoTracking().AsQueryable();
        if (q.Status is { Count: > 0 }) query = query.Where(j => q.Status.Contains(j.Status));
        if (q.Priority is { Count: > 0 }) query = query.Where(j => q.Priority.Contains(j.Priority));
        if (q.RegionId.HasValue) query = query.Where(j => j.RegionId == q.RegionId);
        if (q.TechnicianId.HasValue) query = query.Where(j => j.AssignedTechnicianId == q.TechnicianId);
        if (q.CustomerId.HasValue) query = query.Where(j => j.CustomerId == q.CustomerId);
        if (q.SiteId.HasValue) query = query.Where(j => j.SiteId == q.SiteId);
        if (q.JobTypeId.HasValue) query = query.Where(j => j.JobTypeId == q.JobTypeId);
        if (q.SkillId.HasValue) query = query.Where(j => j.JobType.RequiredSkillId == q.SkillId);
        var from = q.Date ?? q.From;
        var to = q.Date ?? q.To;
        if (from.HasValue) { var f = from.Value.ToDateTime(TimeOnly.MinValue); query = query.Where(j => j.WindowStart >= f); }
        if (to.HasValue) { var t = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue); query = query.Where(j => j.WindowStart < t); }
        if (q.Unassigned == true) query = query.Where(j => j.AssignedTechnicianId == null && j.Status != JobStatus.Cancelled);
        if (q.Unassigned == false) query = query.Where(j => j.AssignedTechnicianId != null);
        if (q.OpenOnly == true) query = query.Where(j => OpenStatuses.Contains(j.Status));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(j => EF.Functions.Like(j.JobNumber, "%" + s + "%") || EF.Functions.Like(j.Summary, "%" + s + "%") || EF.Functions.Like(j.Customer.Name, "%" + s + "%") ||
                                     EF.Functions.Like(j.Site.Name, "%" + s + "%") || EF.Functions.Like(j.Site.City, "%" + s + "%") ||
                                     (j.AssignedTechnician != null && EF.Functions.Like((j.AssignedTechnician.FirstName + " " + j.AssignedTechnician.LastName), "%" + s + "%")));
        }
        return query;
    }

    public async Task<PagedResult<JobListItemDto>> ListAsync(JobQuery q, CancellationToken ct)
    {
        var now = clock.Now;
        var rows = Filter(q).Select(JobProjections.ToRow);
        var sort = JobProjections.NormalizeSort(q.Sort);

        if (q.SlaState is { Count: > 0 })
        {
            // SLA state is computed - filter in memory over the (already narrowed) result set
            var all = await rows.ApplySort(sort, "PriorityRank,WindowStart").Take(20000).ToListAsync(ct);
            var filtered = all.Where(r => q.SlaState.Contains(r.Sla(now).State)).ToList();
            if (string.IsNullOrWhiteSpace(q.Sort)) filtered = filtered.OrderBySlaUrgency(now).ToList();
            var page = filtered.Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize).Select(r => r.ToDto(now)).ToList();
            return new(page, filtered.Count, q.SafePage, q.SafePageSize);
        }

        var paged = await rows.ToPagedAsync(new PageQuery { Page = q.Page, PageSize = q.PageSize, Sort = sort }, r => r,
            "PriorityRank,WindowStart", ct);
        return new(paged.Items.Select(r => r.ToDto(now)).ToList(), paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<JobListItemDto> GetListItemAsync(int id, CancellationToken ct = default)
    {
        var row = await db.Jobs.AsNoTracking().Where(j => j.Id == id).Select(JobProjections.ToRow).SingleOrDefaultAsync(ct)
                  ?? throw ApiException.NotFound("Job", id);
        return row.ToDto(clock.Now);
    }

    public async Task<JobDetailDto> GetDetailAsync(int id, CancellationToken ct = default)
    {
        var item = await GetListItemAsync(id, ct);
        var j = await db.Jobs.AsNoTracking()
            .Include(x => x.Site).Include(x => x.Customer).Include(x => x.Asset).Include(x => x.SlaPolicy).Include(x => x.AssignedByDispatcher)
            .SingleAsync(x => x.Id == id, ct);

        var events = await db.JobEvents.AsNoTracking().Where(e => e.JobId == id).OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Select(e => new JobEventDto(e.Id, e.EventType, e.FromStatus, e.ToStatus, e.TechnicianId,
                e.Technician == null ? null : e.Technician.FirstName + " " + e.Technician.LastName, e.DispatcherId,
                e.Dispatcher == null ? null : e.Dispatcher.FirstName + " " + e.Dispatcher.LastName, e.OccurredAt, e.Details))
            .ToListAsync(ct);
        var notes = await db.JobNotes.AsNoTracking().Where(n => n.JobId == id).OrderByDescending(n => n.CreatedAt)
            .Select(n => new JobNoteDto(n.Id, n.AuthorType, n.AuthorName, n.Body, n.IsInternal, n.CreatedAt)).ToListAsync(ct);
        var parts = await db.JobParts.AsNoTracking().Where(p => p.JobId == id).OrderBy(p => p.Id)
            .Select(p => new JobPartDto(p.Id, p.PartId, p.Part.Sku, p.Part.Name, p.Quantity, p.UnitPrice, p.Quantity * p.UnitPrice, p.AddedAt))
            .ToListAsync(ct);
        var reschedules = await RescheduleProjection(db.Reschedules.AsNoTracking().Where(r => r.JobId == id).OrderByDescending(r => r.CreatedAt))
            .ToListAsync(ct);

        return new JobDetailDto(item, j.Description, j.Source, j.SlaPolicyId, j.SlaPolicy.Name, j.AssetId, j.Asset?.AssetTag,
            j.Asset == null ? null : $"{j.Asset.Manufacturer} {j.Asset.AssetType} ({j.Asset.Model})",
            $"{j.Site.Address}, {j.Site.City}, {j.Site.State} {j.Site.PostalCode}", j.Site.AccessNotes, j.Site.SiteContactPhone,
            j.Customer.Phone, j.Customer.ContactName, j.AssignedByDispatcherId,
            j.AssignedByDispatcher == null ? null : $"{j.AssignedByDispatcher.FirstName} {j.AssignedByDispatcher.LastName}",
            j.AssignedAt, j.OnHoldReason, j.CompletionNotes, j.FirstTimeFix, j.CustomerRating,
            Math.Round(parts.Sum(p => p.LineTotal), 2), events, notes, parts, reschedules);
    }

    public static IQueryable<RescheduleDto> RescheduleProjection(IQueryable<Reschedule> q) => q.Select(r => new RescheduleDto(
        r.Id, r.JobId, r.Job.JobNumber, r.FromTechnicianId,
        r.FromTechnician == null ? null : r.FromTechnician.FirstName + " " + r.FromTechnician.LastName,
        r.ToTechnicianId, r.ToTechnician == null ? null : r.ToTechnician.FirstName + " " + r.ToTechnician.LastName,
        r.FromStart, r.ToStart, r.ReasonCode, r.Comment, r.DispatcherId,
        r.Dispatcher == null ? null : r.Dispatcher.FirstName + " " + r.Dispatcher.LastName, r.CreatedAt));

    public async Task<Job> LoadForUpdateAsync(int id, CancellationToken ct = default) =>
        await db.Jobs.Include(j => j.SlaPolicy).Include(j => j.JobType).Include(j => j.Site).SingleOrDefaultAsync(j => j.Id == id, ct)
        ?? throw ApiException.NotFound("Job", id);

    public async Task<JobDetailDto> CreateAsync(JobCreateDto dto, CancellationToken ct)
    {
        if (dto.WindowEnd <= dto.WindowStart) throw ApiException.BadRequest("WindowEnd must be after WindowStart.");
        var site = await db.Sites.Include(s => s.Customer).SingleOrDefaultAsync(s => s.Id == dto.SiteId, ct) ?? throw ApiException.NotFound("Site", dto.SiteId);
        var jt = await db.JobTypes.FindAsync([dto.JobTypeId], ct) ?? throw ApiException.NotFound("Job type", dto.JobTypeId);
        if (dto.AssetId.HasValue && !await db.Assets.AnyAsync(a => a.Id == dto.AssetId && a.SiteId == site.Id, ct))
            throw ApiException.BadRequest($"Asset {dto.AssetId} does not belong to site {site.Id}.");
        var policy = await db.SlaPolicies.SingleAsync(p => p.Tier == site.Customer.Tier && p.Priority == dto.Priority, ct);
        var now = clock.Now;

        // Reactive work (Critical/High) is measured from creation; appointments are measured against the promised window.
        var responseDue = dto.Priority is JobPriority.Critical or JobPriority.High
            ? now.AddMinutes(policy.ResponseMinutes) : dto.WindowEnd;
        var job = new Job
        {
            JobNumber = "", CustomerId = site.CustomerId, SiteId = site.Id, AssetId = dto.AssetId, JobTypeId = jt.Id, RegionId = site.RegionId,
            SlaPolicyId = policy.Id, Priority = dto.Priority, Status = JobStatus.Unassigned, Source = dto.Source, Summary = dto.Summary,
            Description = dto.Description, CreatedAt = now, WindowStart = dto.WindowStart, WindowEnd = dto.WindowEnd,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes ?? jt.DefaultDurationMinutes, ResponseDueAt = responseDue,
            ResolutionDueAt = responseDue.AddMinutes(policy.ResolutionMinutes - policy.ResponseMinutes), UpdatedAt = now,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        job.JobNumber = $"WO-{dto.WindowStart:yyMMdd}-{job.Id:000000}";
        db.JobEvents.Add(new JobEvent { JobId = job.Id, EventType = JobEventType.Created, ToStatus = JobStatus.Unassigned, DispatcherId = dto.DispatcherId,
            OccurredAt = now, Details = $"Created via {dto.Source}" });
        await db.SaveChangesAsync(ct);
        return await GetDetailAsync(job.Id, ct);
    }

    public async Task<JobDetailDto> UpdateAsync(int id, JobUpdateDto dto, CancellationToken ct)
    {
        var job = await LoadForUpdateAsync(id, ct);
        if (!job.IsOpen) throw ApiException.Conflict($"Job {job.JobNumber} is {job.Status} and cannot be edited.");
        if (dto.Priority.HasValue && dto.Priority != job.Priority)
        {
            var tier = await db.Customers.Where(c => c.Id == job.CustomerId).Select(c => c.Tier).SingleAsync(ct);
            var policy = await db.SlaPolicies.SingleAsync(p => p.Tier == tier && p.Priority == dto.Priority, ct);
            job.Priority = dto.Priority.Value;
            job.SlaPolicyId = policy.Id;
        }
        if (dto.Summary != null) job.Summary = dto.Summary;
        if (dto.Description != null) job.Description = dto.Description;
        if (dto.WindowStart.HasValue) job.WindowStart = dto.WindowStart.Value;
        if (dto.WindowEnd.HasValue) job.WindowEnd = dto.WindowEnd.Value;
        if (job.WindowEnd <= job.WindowStart) throw ApiException.BadRequest("WindowEnd must be after WindowStart.");
        if (dto.EstimatedDurationMinutes.HasValue)
        {
            job.EstimatedDurationMinutes = dto.EstimatedDurationMinutes.Value;
            if (job.ScheduledStart.HasValue) job.ScheduledEnd = job.ScheduledStart.Value.AddMinutes(job.EstimatedDurationMinutes);
        }
        if (dto.AssetId.HasValue) job.AssetId = dto.AssetId;
        job.UpdatedAt = clock.Now;
        await db.SaveChangesAsync(ct);
        return await GetDetailAsync(id, ct);
    }

    public async Task<JobDetailDto> ChangeStatusAsync(int id, StatusChangeDto dto, CancellationToken ct)
    {
        var job = await LoadForUpdateAsync(id, ct);
        var from = job.Status;
        if (from == dto.Status) return await GetDetailAsync(id, ct);
        if (!Transitions[from].Contains(dto.Status))
            throw ApiException.Conflict($"Cannot change status from {from} to {dto.Status}.",
                new { allowed = Transitions[from].Select(s => s.ToString()) });
        var target = dto.Status;
        if (target is JobStatus.Assigned or JobStatus.Dispatched or JobStatus.EnRoute or JobStatus.OnSite && job.AssignedTechnicianId == null)
            throw ApiException.Conflict($"Job must be assigned to a technician before moving to {target}. Use POST /api/jobs/{id}/assign.");

        var now = clock.Now;
        var tech = job.AssignedTechnicianId.HasValue ? await db.Technicians.FindAsync([job.AssignedTechnicianId.Value], ct) : null;
        switch (target)
        {
            case JobStatus.EnRoute:
                if (tech != null) tech.FieldStatus = FieldStatus.EnRoute;
                break;
            case JobStatus.OnSite:
                job.ActualStart ??= now;
                job.OnHoldReason = null;
                if (tech != null)
                {
                    tech.FieldStatus = FieldStatus.OnSite;
                    tech.CurrentLatitude = job.Site.Latitude; tech.CurrentLongitude = job.Site.Longitude; tech.LastLocationAt = now;
                }
                break;
            case JobStatus.OnHold:
                job.OnHoldReason = dto.OnHoldReason ?? dto.Note ?? "On hold";
                if (tech != null && from == JobStatus.OnSite) tech.FieldStatus = FieldStatus.Available;
                break;
            case JobStatus.Completed:
                job.ActualEnd = now;
                job.CompletionNotes = dto.CompletionNotes ?? dto.Note;
                job.FirstTimeFix = dto.FirstTimeFix ?? true;
                if (tech != null) tech.FieldStatus = FieldStatus.Available;
                break;
            case JobStatus.Cancelled:
                if (tech != null && from is JobStatus.EnRoute) tech.FieldStatus = FieldStatus.Available;
                break;
            case JobStatus.Unassigned:
                job.AssignedTechnicianId = null; job.ScheduledStart = null; job.ScheduledEnd = null;
                break;
        }
        job.Status = target;
        job.UpdatedAt = now;
        db.JobEvents.Add(new JobEvent { JobId = id, EventType = JobEventType.StatusChanged, FromStatus = from, ToStatus = target,
            TechnicianId = job.AssignedTechnicianId, DispatcherId = dto.DispatcherId, OccurredAt = now, Details = dto.Note });
        if (target == JobStatus.Completed && job.ActualEnd > job.ResolutionDueAt)
            db.JobEvents.Add(new JobEvent { JobId = id, EventType = JobEventType.SlaBreached, OccurredAt = now, Details = "Resolution SLA breached" });
        if (target == JobStatus.OnSite && job.ActualStart > job.ResponseDueAt)
            db.JobEvents.Add(new JobEvent { JobId = id, EventType = JobEventType.SlaBreached, OccurredAt = now, Details = "Response SLA breached" });
        await db.SaveChangesAsync(ct);
        return await GetDetailAsync(id, ct);
    }

    public async Task<JobNoteDto> AddNoteAsync(int id, NoteCreateDto dto, CancellationToken ct)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == id, ct)) throw ApiException.NotFound("Job", id);
        var now = clock.Now;
        var note = new JobNote { JobId = id, AuthorType = dto.AuthorType, AuthorName = dto.AuthorName, Body = dto.Body, IsInternal = dto.IsInternal, CreatedAt = now };
        db.JobNotes.Add(note);
        db.JobEvents.Add(new JobEvent { JobId = id, EventType = JobEventType.NoteAdded, OccurredAt = now, Details = $"{dto.AuthorName}: {Truncate(dto.Body, 80)}" });
        await db.SaveChangesAsync(ct);
        return new JobNoteDto(note.Id, note.AuthorType, note.AuthorName, note.Body, note.IsInternal, note.CreatedAt);
    }

    public async Task<JobPartDto> AddPartAsync(int id, JobPartCreateDto dto, CancellationToken ct)
    {
        var job = await LoadForUpdateAsync(id, ct);
        var part = await db.Parts.FindAsync([dto.PartId], ct) ?? throw ApiException.NotFound("Part", dto.PartId);
        var now = clock.Now;
        var jp = new JobPart { JobId = id, PartId = part.Id, Quantity = dto.Quantity, UnitPrice = part.UnitPrice, AddedAt = now };
        db.JobParts.Add(jp);
        // Draw down inventory at the technician's depot, if assigned
        if (job.AssignedTechnicianId.HasValue)
        {
            var depotId = await db.Technicians.Where(t => t.Id == job.AssignedTechnicianId).Select(t => t.DepotId).SingleAsync(ct);
            var inv = await db.DepotInventory.FindAsync([depotId, part.Id], ct);
            if (inv != null) inv.QuantityOnHand = Math.Max(0, inv.QuantityOnHand - dto.Quantity);
        }
        db.JobEvents.Add(new JobEvent { JobId = id, EventType = JobEventType.PartsUsed, TechnicianId = job.AssignedTechnicianId, OccurredAt = now,
            Details = $"{dto.Quantity} x {part.Name}" });
        await db.SaveChangesAsync(ct);
        return new JobPartDto(jp.Id, part.Id, part.Sku, part.Name, jp.Quantity, jp.UnitPrice, jp.Quantity * jp.UnitPrice, jp.AddedAt);
    }

    public async Task RemovePartAsync(int jobId, int jobPartId, CancellationToken ct)
    {
        var jp = await db.JobParts.SingleOrDefaultAsync(p => p.Id == jobPartId && p.JobId == jobId, ct) ?? throw ApiException.NotFound("Job part", jobPartId);
        db.JobParts.Remove(jp);
        await db.SaveChangesAsync(ct);
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
