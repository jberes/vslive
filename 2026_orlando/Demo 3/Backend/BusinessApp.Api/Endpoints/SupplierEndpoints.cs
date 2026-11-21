using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class SupplierDto
{
    public int Id { get; init; }
    public required string CompanyName { get; init; }
    public string? ContactName { get; init; }
    public string? ContactTitle { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Phone { get; init; }
    public string? Fax { get; init; }
    public string? HomePage { get; init; }
    public int ProductCount { get; init; }
    public double TotalSales { get; init; }
}

public sealed class SupplierInput
{
    [Required, StringLength(128)] public string CompanyName { get; set; } = "";
    [StringLength(64)] public string? ContactName { get; set; }
    [StringLength(64)] public string? ContactTitle { get; set; }
    [StringLength(128)] public string? Address { get; set; }
    [StringLength(64)] public string? City { get; set; }
    [StringLength(64)] public string? Region { get; set; }
    [StringLength(16)] public string? PostalCode { get; set; }
    [StringLength(64)] public string? Country { get; set; }
    [StringLength(32)] public string? Phone { get; set; }
    [StringLength(32)] public string? Fax { get; set; }
    [Url, StringLength(256)] public string? HomePage { get; set; }
}

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/suppliers").WithTags("Suppliers");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Suppliers).ToPagedResultAsync(q, s => x =>
                x.CompanyName.ToLower().Contains(s) || (x.ContactName ?? "").ToLower().Contains(s) ||
                (x.City ?? "").ToLower().Contains(s) || (x.Country ?? "").ToLower().Contains(s), "companyName", ct));

        g.MapGet("/lookup", async (AppDbContext db, CancellationToken ct) =>
            await db.Suppliers.OrderBy(s => s.CompanyName).Select(s => new LookupItem(s.Id, s.CompanyName)).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Suppliers.Where(s => s.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/products", async (int id, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await ProductEndpoints.Project(db.Products.Where(p => p.SupplierId == id)).ToPagedResultAsync(q, ProductEndpoints.Search, "name", ct));

        g.MapPost("/", async (SupplierInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = new Supplier { CompanyName = input.CompanyName };
            Apply(entity, input);
            db.Suppliers.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/suppliers/{entity.Id}", await Project(db.Suppliers.Where(s => s.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, SupplierInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Suppliers.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            Apply(entity, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Suppliers.Where(s => s.Id == id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.Products.AnyAsync(p => p.SupplierId == id, ct))
                return Results.Problem("Supplier still has products.", statusCode: 409);
            return await db.Suppliers.Where(s => s.Id == id).ExecuteDeleteAsync(ct) > 0 ? Results.NoContent() : Results.NotFound();
        });
    }

    private static IQueryable<SupplierDto> Project(IQueryable<Supplier> q) => q.Select(s => new SupplierDto
    {
        Id = s.Id,
        CompanyName = s.CompanyName,
        ContactName = s.ContactName,
        ContactTitle = s.ContactTitle,
        Address = s.Address,
        City = s.City,
        Region = s.Region,
        PostalCode = s.PostalCode,
        Country = s.Country,
        Phone = s.Phone,
        Fax = s.Fax,
        HomePage = s.HomePage,
        ProductCount = s.Products.Count,
        TotalSales = Math.Round(s.Products.SelectMany(p => p.OrderDetails).Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
    });

    private static void Apply(Supplier e, SupplierInput i)
    {
        e.CompanyName = i.CompanyName.Trim();
        e.ContactName = i.ContactName;
        e.ContactTitle = i.ContactTitle;
        e.Address = i.Address;
        e.City = i.City;
        e.Region = i.Region;
        e.PostalCode = i.PostalCode;
        e.Country = i.Country;
        e.Phone = i.Phone;
        e.Fax = i.Fax;
        e.HomePage = i.HomePage;
    }
}
