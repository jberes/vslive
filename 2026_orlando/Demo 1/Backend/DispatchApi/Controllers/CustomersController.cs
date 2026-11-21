using System.Linq.Expressions;
using DispatchApi.Data;
using DispatchApi.Dtos;
using DispatchApi.Infrastructure;
using DispatchApi.Models;
using DispatchApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Controllers;

public class CustomerQuery : PageQuery
{
    public CustomerTier? Tier { get; set; }
    public CustomerType? CustomerType { get; set; }
    public int? RegionId { get; set; }
    public bool? IsActive { get; set; }
}

[ApiController, Route("api/customers"), Tags("Customers")]
public class CustomersController(DispatchDbContext db, IClock clock, JobService jobs) : ControllerBase
{
    /// <summary>Paged customers. Sort e.g. "name", "-tier", "accountNumber".</summary>
    [HttpGet]
    public Task<PagedResult<CustomerListItemDto>> List([FromQuery] CustomerQuery q, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (q.Tier.HasValue) query = query.Where(c => c.Tier == q.Tier);
        if (q.CustomerType.HasValue) query = query.Where(c => c.CustomerType == q.CustomerType);
        if (q.IsActive.HasValue) query = query.Where(c => c.IsActive == q.IsActive);
        if (q.RegionId.HasValue) query = query.Where(c => c.Sites.Any(s => s.RegionId == q.RegionId));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(c => EF.Functions.Like(c.Name, "%" + q.Search + "%") || EF.Functions.Like(c.AccountNumber, "%" + q.Search + "%") || EF.Functions.Like(c.ContactName, "%" + q.Search + "%") || EF.Functions.Like(c.Email, "%" + q.Search + "%"));
        return query.ToPagedAsync(q, c => new CustomerListItemDto(c.Id, c.AccountNumber, c.Name, c.CustomerType, c.Tier, c.ContactName, c.Phone, c.Email,
            c.IsActive, c.Sites.Count, db.Jobs.Count(j => j.CustomerId == c.Id && JobService.OpenStatuses.Contains(j.Status))), "Name", ct);
    }

    [HttpGet("{id:int}")]
    public async Task<CustomerDetailDto> Get(int id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("Customer", id);
        var sites = await SitesController.Query(db, s => s.CustomerId == id).ToListAsync(ct);
        var stats = await db.Jobs.AsNoTracking().Where(j => j.CustomerId == id)
            .Select(j => new { j.Status, j.CustomerRating, j.ActualStart, j.ActualEnd, j.ResponseDueAt, j.ResolutionDueAt }).ToListAsync(ct);
        var completed = stats.Where(s => s.Status == JobStatus.Completed).ToList();
        return new CustomerDetailDto(c.Id, c.AccountNumber, c.Name, c.CustomerType, c.Tier, c.ContactName, c.Phone, c.Email, c.CreatedAt, c.IsActive, sites,
            stats.Count, stats.Count(s => JobService.OpenStatuses.Contains(s.Status)),
            completed.Any(s => s.CustomerRating.HasValue) ? Math.Round(completed.Where(s => s.CustomerRating.HasValue).Average(s => s.CustomerRating!.Value), 2) : null,
            completed.Count == 0 ? null : Math.Round(completed.Count(s => s.ActualStart <= s.ResponseDueAt && s.ActualEnd <= s.ResolutionDueAt) * 100.0 / completed.Count, 1));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDetailDto>> Create(CustomerUpsertDto dto, CancellationToken ct)
    {
        if (await db.Customers.AnyAsync(c => c.AccountNumber == dto.AccountNumber, ct)) throw ApiException.Conflict("Account number already exists.");
        var c = new Customer { CreatedAt = clock.Now };
        Apply(c, dto);
        db.Customers.Add(c);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = c.Id }, await Get(c.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<CustomerDetailDto> Update(int id, CustomerUpsertDto dto, CancellationToken ct)
    {
        var c = await db.Customers.FindAsync([id], ct) ?? throw ApiException.NotFound("Customer", id);
        if (await db.Customers.AnyAsync(x => x.Id != id && x.AccountNumber == dto.AccountNumber, ct)) throw ApiException.Conflict("Account number already exists.");
        Apply(c, dto);
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    private static void Apply(Customer c, CustomerUpsertDto d)
    {
        c.AccountNumber = d.AccountNumber; c.Name = d.Name; c.CustomerType = d.CustomerType; c.Tier = d.Tier; c.ContactName = d.ContactName;
        c.Phone = d.Phone; c.Email = d.Email; c.IsActive = d.IsActive;
    }

    /// <summary>Soft delete (deactivate). Blocked while the customer has open jobs.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var c = await db.Customers.FindAsync([id], ct) ?? throw ApiException.NotFound("Customer", id);
        if (await db.Jobs.AnyAsync(j => j.CustomerId == id && JobService.OpenStatuses.Contains(j.Status), ct))
            throw ApiException.Conflict("Customer has open jobs.");
        c.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:int}/sites")]
    public Task<List<SiteDto>> Sites(int id, CancellationToken ct) => SitesController.Query(db, s => s.CustomerId == id).ToListAsync(ct);

    /// <summary>Job history for a customer (all job filters supported).</summary>
    [HttpGet("{id:int}/jobs")]
    public Task<PagedResult<JobListItemDto>> Jobs(int id, [FromQuery] JobQuery q, CancellationToken ct)
    {
        q.CustomerId = id;
        q.Sort ??= "-windowStart";
        return jobs.ListAsync(q, ct);
    }
}

public class SiteQuery : PageQuery
{
    public int? CustomerId { get; set; }
    public int? RegionId { get; set; }
    public string? City { get; set; }
}

[ApiController, Route("api/sites"), Tags("Customers")]
public class SitesController(DispatchDbContext db, JobService jobs) : ControllerBase
{
    internal static IQueryable<SiteDto> Query(DispatchDbContext db, Expression<Func<Site, bool>>? where = null) => db.Sites.AsNoTracking().Where(where ?? (_ => true)).OrderBy(s => s.Name).Select(s => new SiteDto(s.Id, s.CustomerId,
        s.Customer.Name, s.Customer.Tier, s.RegionId, s.Region.Name, s.Name, s.Address, s.City, s.State, s.PostalCode, s.Latitude, s.Longitude,
        s.AccessNotes, s.SiteContactPhone, s.Assets.Count));

    [HttpGet]
    public Task<PagedResult<SiteDto>> List([FromQuery] SiteQuery q, CancellationToken ct)
    {
        var query = db.Sites.AsNoTracking().AsQueryable();
        if (q.CustomerId.HasValue) query = query.Where(s => s.CustomerId == q.CustomerId);
        if (q.RegionId.HasValue) query = query.Where(s => s.RegionId == q.RegionId);
        if (!string.IsNullOrWhiteSpace(q.City)) query = query.Where(s => s.City == q.City);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(s => EF.Functions.Like(s.Name, "%" + q.Search + "%") || EF.Functions.Like(s.Address, "%" + q.Search + "%") || EF.Functions.Like(s.Customer.Name, "%" + q.Search + "%") || EF.Functions.Like(s.PostalCode, "%" + q.Search + "%"));
        return query.ToPagedAsync(q, s => new SiteDto(s.Id, s.CustomerId, s.Customer.Name, s.Customer.Tier, s.RegionId, s.Region.Name, s.Name, s.Address,
            s.City, s.State, s.PostalCode, s.Latitude, s.Longitude, s.AccessNotes, s.SiteContactPhone, s.Assets.Count), "Name", ct);
    }

    [HttpGet("{id:int}")]
    public async Task<SiteDto> Get(int id, CancellationToken ct) =>
        await Query(db, s => s.Id == id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Site", id);

    [HttpPost]
    public async Task<ActionResult<SiteDto>> Create(SiteUpsertDto dto, CancellationToken ct)
    {
        await Validate(dto, ct);
        var s = new Site();
        Apply(s, dto);
        db.Sites.Add(s);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = s.Id }, await Get(s.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<SiteDto> Update(int id, SiteUpsertDto dto, CancellationToken ct)
    {
        var s = await db.Sites.FindAsync([id], ct) ?? throw ApiException.NotFound("Site", id);
        await Validate(dto, ct);
        Apply(s, dto);
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var s = await db.Sites.FindAsync([id], ct) ?? throw ApiException.NotFound("Site", id);
        if (await db.Jobs.AnyAsync(j => j.SiteId == id, ct)) throw ApiException.Conflict("Site has job history and cannot be deleted.");
        db.Sites.Remove(s);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task Validate(SiteUpsertDto d, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == d.CustomerId, ct)) throw ApiException.BadRequest($"Customer {d.CustomerId} does not exist.");
        if (!await db.Regions.AnyAsync(r => r.Id == d.RegionId, ct)) throw ApiException.BadRequest($"Region {d.RegionId} does not exist.");
    }

    private static void Apply(Site s, SiteUpsertDto d)
    {
        s.CustomerId = d.CustomerId; s.RegionId = d.RegionId; s.Name = d.Name; s.Address = d.Address; s.City = d.City; s.State = d.State;
        s.PostalCode = d.PostalCode; s.Latitude = d.Latitude; s.Longitude = d.Longitude; s.AccessNotes = d.AccessNotes; s.SiteContactPhone = d.SiteContactPhone;
    }

    [HttpGet("{id:int}/assets")]
    public Task<List<AssetDto>> Assets(int id, CancellationToken ct) => AssetsController.Query(db, a => a.SiteId == id).ToListAsync(ct);

    [HttpGet("{id:int}/jobs")]
    public Task<PagedResult<JobListItemDto>> Jobs(int id, [FromQuery] JobQuery q, CancellationToken ct)
    {
        q.SiteId = id;
        q.Sort ??= "-windowStart";
        return jobs.ListAsync(q, ct);
    }
}

[ApiController, Route("api/assets"), Tags("Customers")]
public class AssetsController(DispatchDbContext db, IClock clock) : ControllerBase
{
    internal static IQueryable<AssetDto> Query(DispatchDbContext db, Expression<Func<Asset, bool>>? where = null) => db.Assets.AsNoTracking().Where(where ?? (_ => true)).OrderBy(a => a.AssetTag).Select(a => new AssetDto(a.Id,
        a.SiteId, a.Site.Name, a.Site.CustomerId, a.AssetTag, a.AssetType, a.Manufacturer, a.Model, a.SerialNumber, a.InstallDate, a.WarrantyExpires,
        a.WarrantyExpires != null && a.WarrantyExpires >= DateOnly.FromDateTime(DateTime.Today), a.Status));

    [HttpGet]
    public Task<PagedResult<AssetDto>> List([FromQuery] PageQuery q, [FromQuery] int? siteId, [FromQuery] int? customerId,
        [FromQuery] AssetStatus? status, CancellationToken ct)
    {
        var today = clock.Today;
        var query = db.Assets.AsNoTracking().AsQueryable();
        if (siteId.HasValue) query = query.Where(a => a.SiteId == siteId);
        if (customerId.HasValue) query = query.Where(a => a.Site.CustomerId == customerId);
        if (status.HasValue) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(a => EF.Functions.Like(a.AssetTag, "%" + q.Search + "%") || EF.Functions.Like(a.SerialNumber, "%" + q.Search + "%") || EF.Functions.Like(a.AssetType, "%" + q.Search + "%") || EF.Functions.Like(a.Manufacturer, "%" + q.Search + "%"));
        return query.ToPagedAsync(q, a => new AssetDto(a.Id, a.SiteId, a.Site.Name, a.Site.CustomerId, a.AssetTag, a.AssetType, a.Manufacturer, a.Model,
            a.SerialNumber, a.InstallDate, a.WarrantyExpires, a.WarrantyExpires != null && a.WarrantyExpires >= today, a.Status), "AssetTag", ct);
    }

    [HttpGet("{id:int}")]
    public async Task<AssetDto> Get(int id, CancellationToken ct) =>
        await Query(db, a => a.Id == id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Asset", id);

    [HttpPost]
    public async Task<ActionResult<AssetDto>> Create(AssetUpsertDto dto, CancellationToken ct)
    {
        if (!await db.Sites.AnyAsync(s => s.Id == dto.SiteId, ct)) throw ApiException.BadRequest($"Site {dto.SiteId} does not exist.");
        if (await db.Assets.AnyAsync(a => a.AssetTag == dto.AssetTag, ct)) throw ApiException.Conflict("Asset tag already exists.");
        var a = new Asset();
        Apply(a, dto);
        db.Assets.Add(a);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = a.Id }, await Get(a.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<AssetDto> Update(int id, AssetUpsertDto dto, CancellationToken ct)
    {
        var a = await db.Assets.FindAsync([id], ct) ?? throw ApiException.NotFound("Asset", id);
        if (await db.Assets.AnyAsync(x => x.Id != id && x.AssetTag == dto.AssetTag, ct)) throw ApiException.Conflict("Asset tag already exists.");
        Apply(a, dto);
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var a = await db.Assets.FindAsync([id], ct) ?? throw ApiException.NotFound("Asset", id);
        if (await db.Jobs.AnyAsync(j => j.AssetId == id, ct))
        {
            a.Status = AssetStatus.Retired; // keep history intact
        }
        else db.Assets.Remove(a);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(Asset a, AssetUpsertDto d)
    {
        a.SiteId = d.SiteId; a.AssetTag = d.AssetTag; a.AssetType = d.AssetType; a.Manufacturer = d.Manufacturer; a.Model = d.Model;
        a.SerialNumber = d.SerialNumber; a.InstallDate = d.InstallDate; a.WarrantyExpires = d.WarrantyExpires; a.Status = d.Status;
    }
}
