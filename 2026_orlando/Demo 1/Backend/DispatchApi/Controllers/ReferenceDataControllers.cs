using System.Linq.Expressions;
using DispatchApi.Data;
using DispatchApi.Dtos;
using DispatchApi.Infrastructure;
using DispatchApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Controllers;

/// <summary>Lookups for dropdowns/filters and the operations clock.</summary>
[ApiController, Route("api/meta"), Tags("Meta")]
public class MetaController(DispatchDbContext db, IClock clock) : ControllerBase
{
    /// <summary>All reference data plus every enum's allowed values, in one call (ideal for app bootstrap).</summary>
    [HttpGet("lookups")]
    public async Task<LookupsDto> Lookups(CancellationToken ct)
    {
        var enums = new Dictionary<string, string[]>
        {
            ["jobStatus"] = Enum.GetNames<JobStatus>(), ["jobPriority"] = Enum.GetNames<JobPriority>(), ["jobSource"] = Enum.GetNames<JobSource>(),
            ["jobCategory"] = Enum.GetNames<JobCategory>(), ["customerTier"] = Enum.GetNames<CustomerTier>(), ["customerType"] = Enum.GetNames<CustomerType>(),
            ["technicianLevel"] = Enum.GetNames<TechnicianLevel>(), ["employmentStatus"] = Enum.GetNames<EmploymentStatus>(),
            ["fieldStatus"] = Enum.GetNames<FieldStatus>(), ["shiftType"] = Enum.GetNames<ShiftType>(), ["shiftStatus"] = Enum.GetNames<ShiftStatus>(),
            ["timeOffType"] = Enum.GetNames<TimeOffType>(), ["timeOffStatus"] = Enum.GetNames<TimeOffStatus>(), ["assetStatus"] = Enum.GetNames<AssetStatus>(),
            ["rescheduleReason"] = Enum.GetNames<RescheduleReason>(), ["slaState"] = Enum.GetNames<SlaState>(), ["noteAuthorType"] = Enum.GetNames<NoteAuthorType>(),
            ["reassignMode"] = Enum.GetNames<ReassignMode>(), ["vehicleStatus"] = Enum.GetNames<VehicleStatus>(),
        };
        return new LookupsDto(
            await RegionsController.Query(db).ToListAsync(ct),
            await DepotsController.Query(db).ToListAsync(ct),
            await db.Skills.AsNoTracking().OrderBy(s => s.Name).Select(s => new SkillDto(s.Id, s.Code, s.Name, s.Category, s.RequiresCertification)).ToListAsync(ct),
            await JobTypesController.Query(db).ToListAsync(ct),
            await SlaPoliciesController.Query(db).ToListAsync(ct),
            enums);
    }

    /// <summary>Current operations time. In Simulated mode the clock starts at 11:30 today when the API boots.</summary>
    [HttpGet("clock")]
    public async Task<ClockDto> Clock(CancellationToken ct)
    {
        var meta = await db.SeedMeta.AsNoTracking().SingleAsync(ct);
        return new ClockDto(clock.Now, clock.Today, clock.IsSimulated, meta.AnchorDate, meta.GeneratorVersion);
    }
}

[ApiController, Route("api/regions"), Tags("Reference data")]
public class RegionsController(DispatchDbContext db) : ControllerBase
{
    internal static IQueryable<RegionDto> Query(DispatchDbContext db, Expression<Func<Region, bool>>? where = null) => db.Regions.AsNoTracking().Where(where ?? (_ => true)).OrderBy(r => r.Name)
        .Select(r => new RegionDto(r.Id, r.Code, r.Name, r.TimeZone, r.CenterLat, r.CenterLng, r.Depots.Count));

    [HttpGet] public Task<List<RegionDto>> List(CancellationToken ct) => Query(db).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<RegionDto> Get(int id, CancellationToken ct) =>
        await Query(db, r => r.Id == id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Region", id);
}

[ApiController, Route("api/depots"), Tags("Reference data")]
public class DepotsController(DispatchDbContext db) : ControllerBase
{
    internal static IQueryable<DepotDto> Query(DispatchDbContext db, Expression<Func<Depot, bool>>? where = null) => db.Depots.AsNoTracking().Where(where ?? (_ => true)).OrderBy(d => d.Name)
        .Select(d => new DepotDto(d.Id, d.RegionId, d.Region.Name, d.Code, d.Name, d.Address, d.City, d.State, d.PostalCode, d.Latitude, d.Longitude, d.Phone));

    [HttpGet]
    public Task<List<DepotDto>> List([FromQuery] int? regionId, CancellationToken ct) =>
        Query(db, d => regionId == null || d.RegionId == regionId).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<DepotDto> Get(int id, CancellationToken ct) =>
        await Query(db, d => d.Id == id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Depot", id);

    /// <summary>Parts stock at a depot.</summary>
    [HttpGet("{id:int}/inventory")]
    public Task<List<InventoryDto>> Inventory(int id, [FromQuery] bool belowReorderOnly, [FromQuery] string? category, CancellationToken ct) =>
        db.DepotInventory.AsNoTracking().Where(i => i.DepotId == id && (!belowReorderOnly || i.QuantityOnHand < i.ReorderLevel) &&
                                                   (category == null || i.Part.Category == category))
            .OrderBy(i => i.Part.Category).ThenBy(i => i.Part.Name)
            .Select(i => new InventoryDto(i.DepotId, i.Depot.Name, i.PartId, i.Part.Sku, i.Part.Name, i.Part.Category, i.QuantityOnHand, i.ReorderLevel,
                i.QuantityOnHand < i.ReorderLevel))
            .ToListAsync(ct);
}

[ApiController, Route("api/skills"), Tags("Reference data")]
public class SkillsController(DispatchDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<SkillDto>> List(CancellationToken ct) => db.Skills.AsNoTracking().OrderBy(s => s.Category).ThenBy(s => s.Name)
        .Select(s => new SkillDto(s.Id, s.Code, s.Name, s.Category, s.RequiresCertification)).ToListAsync(ct);
}

[ApiController, Route("api/job-types"), Tags("Reference data")]
public class JobTypesController(DispatchDbContext db) : ControllerBase
{
    internal static IQueryable<JobTypeDto> Query(DispatchDbContext db) => db.JobTypes.AsNoTracking().OrderBy(j => j.Name)
        .Select(j => new JobTypeDto(j.Id, j.Code, j.Name, j.Category, j.RequiredSkillId, j.RequiredSkill.Name, j.MinProficiency, j.DefaultDurationMinutes, j.BasePrice));

    [HttpGet] public Task<List<JobTypeDto>> List(CancellationToken ct) => Query(db).ToListAsync(ct);
}

[ApiController, Route("api/sla-policies"), Tags("Reference data")]
public class SlaPoliciesController(DispatchDbContext db) : ControllerBase
{
    internal static IQueryable<SlaPolicyDto> Query(DispatchDbContext db) => db.SlaPolicies.AsNoTracking().OrderBy(p => p.Id)
        .Select(p => new SlaPolicyDto(p.Id, p.Name, p.Tier, p.Priority, p.ResponseMinutes, p.ResolutionMinutes, p.AtRiskThresholdMinutes, p.PenaltyAmount));

    [HttpGet] public Task<List<SlaPolicyDto>> List(CancellationToken ct) => Query(db).ToListAsync(ct);
}

[ApiController, Route("api/parts"), Tags("Reference data")]
public class PartsController(DispatchDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<PartDto>> List([FromQuery] string? category, [FromQuery] string? search, CancellationToken ct) =>
        db.Parts.AsNoTracking().Where(p => (category == null || p.Category == category) &&
                                           (search == null || EF.Functions.Like(p.Name, "%" + search + "%") || EF.Functions.Like(p.Sku, "%" + search + "%")))
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .Select(p => new PartDto(p.Id, p.Sku, p.Name, p.Category, p.UnitCost, p.UnitPrice)).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<PartDto> Get(int id, CancellationToken ct) =>
        await db.Parts.AsNoTracking().Where(p => p.Id == id).Select(p => new PartDto(p.Id, p.Sku, p.Name, p.Category, p.UnitCost, p.UnitPrice))
            .SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Part", id);
}

[ApiController, Route("api/vehicles"), Tags("Reference data")]
public class VehiclesController(DispatchDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<VehicleDto>> List([FromQuery] int? depotId, [FromQuery] VehicleStatus? status, CancellationToken ct) =>
        db.Vehicles.AsNoTracking().Where(v => (depotId == null || v.DepotId == depotId) && (status == null || v.Status == status))
            .OrderBy(v => v.PlateNumber)
            .Select(v => new VehicleDto(v.Id, v.DepotId, v.Depot.Name, v.PlateNumber, v.Make, v.Model, v.Year, v.VehicleType, v.Status, v.OdometerMiles,
                db.Technicians.Where(t => t.VehicleId == v.Id).Select(t => (int?)t.Id).FirstOrDefault(),
                db.Technicians.Where(t => t.VehicleId == v.Id).Select(t => t.FirstName + " " + t.LastName).FirstOrDefault()))
            .ToListAsync(ct);
}

[ApiController, Route("api/dispatchers"), Tags("Dispatchers")]
public class DispatchersController(DispatchDbContext db) : ControllerBase
{
    private IQueryable<DispatcherDto> Query(Expression<Func<Dispatcher, bool>>? where = null) => db.Dispatchers.AsNoTracking().Where(where ?? (_ => true)).OrderBy(d => d.LastName)
        .Select(d => new DispatcherDto(d.Id, d.EmployeeNumber, d.FirstName, d.LastName, d.FirstName + " " + d.LastName, d.Email, d.Phone,
            d.RegionId, d.Region.Name, d.ShiftName, d.Role, d.IsActive));

    /// <summary>The 40 dispatchers using the console.</summary>
    [HttpGet]
    public Task<List<DispatcherDto>> List([FromQuery] int? regionId, CancellationToken ct) =>
        Query(d => regionId == null || d.RegionId == regionId).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<DispatcherDto> Get(int id, CancellationToken ct) =>
        await Query(d => d.Id == id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Dispatcher", id);

    /// <summary>Recent activity (assignments, reschedules, status changes) performed by a dispatcher.</summary>
    [HttpGet("{id:int}/activity")]
    public Task<List<JobEventDto>> Activity(int id, [FromQuery] int limit = 100, CancellationToken ct = default) =>
        db.JobEvents.AsNoTracking().Where(e => e.DispatcherId == id).OrderByDescending(e => e.OccurredAt).Take(Math.Clamp(limit, 1, 1000))
            .Select(e => new JobEventDto(e.Id, e.EventType, e.FromStatus, e.ToStatus, e.TechnicianId,
                e.Technician == null ? null : e.Technician.FirstName + " " + e.Technician.LastName, e.DispatcherId,
                e.Dispatcher == null ? null : e.Dispatcher.FirstName + " " + e.Dispatcher.LastName, e.OccurredAt, e.Job.JobNumber + ": " + e.Details))
            .ToListAsync(ct);
}
