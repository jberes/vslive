using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class ShipperDto
{
    public int Id { get; init; }
    public required string CompanyName { get; init; }
    public string? Phone { get; init; }
    public int OrderCount { get; init; }
    public int PendingShipments { get; init; }
    public double TotalFreight { get; init; }
    public double AverageFreight { get; init; }
    public double AverageDaysToShip { get; init; }
    public int LateShipments { get; init; }
}

public sealed class ShipperInput
{
    [Required, StringLength(64)] public string CompanyName { get; set; } = "";
    [StringLength(32)] public string? Phone { get; set; }
}

public static class ShipperEndpoints
{
    public static void MapShipperEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/shippers").WithTags("Shippers");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Shippers).ToPagedResultAsync(q, s => x => x.CompanyName.ToLower().Contains(s), "companyName", ct));

        g.MapGet("/lookup", async (AppDbContext db, CancellationToken ct) =>
            await db.Shippers.OrderBy(s => s.CompanyName).Select(s => new LookupItem(s.Id, s.CompanyName)).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Shippers.Where(s => s.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/orders", async (int id, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await OrderEndpoints.Project(db.Orders.Where(o => o.ShipViaId == id)).ToPagedResultAsync(q, OrderEndpoints.Search, "-orderDate", ct));

        g.MapGet("/{id:int}/dashboard", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var shipper = await Project(db.Shippers.Where(s => s.Id == id)).FirstOrDefaultAsync(ct);
            if (shipper is null) return Results.NotFound();
            var orders = db.Orders.Where(o => o.ShipViaId == id);
            var byYear = await orders.GroupBy(o => o.OrderDate.Year)
                .Select(g => new { Year = g.Key, Orders = g.Count(), Freight = g.Sum(o => (double)o.Freight) })
                .OrderBy(x => x.Year).ToListAsync(ct);
            var byCountry = await orders.GroupBy(o => o.ShipCountry ?? "Unknown")
                .Select(g => new { Country = g.Key, Orders = g.Count() })
                .OrderByDescending(x => x.Orders).Take(10).ToListAsync(ct);
            var share = await db.Shippers.Select(s => new NameValue(s.CompanyName, s.Orders.Count)).ToListAsync(ct);
            return Results.Ok(new
            {
                shipper,
                byYear = byYear.Select(x => new { year = x.Year, orders = x.Orders, freight = Money.Round(x.Freight) }),
                topCountries = byCountry.Select(x => new NameValue(x.Country, x.Orders)),
                marketShare = share,
            });
        });

        g.MapPost("/", async (ShipperInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = new Shipper { CompanyName = input.CompanyName.Trim(), Phone = input.Phone };
            db.Shippers.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/shippers/{entity.Id}", await Project(db.Shippers.Where(s => s.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, ShipperInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Shippers.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            entity.CompanyName = input.CompanyName.Trim();
            entity.Phone = input.Phone;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Shippers.Where(s => s.Id == id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.Orders.AnyAsync(o => o.ShipViaId == id, ct))
                return Results.Problem("Shipper is used by existing orders.", statusCode: 409);
            return await db.Shippers.Where(s => s.Id == id).ExecuteDeleteAsync(ct) > 0 ? Results.NoContent() : Results.NotFound();
        });
    }

    private static IQueryable<ShipperDto> Project(IQueryable<Shipper> q) => q.Select(s => new ShipperDto
    {
        Id = s.Id,
        CompanyName = s.CompanyName,
        Phone = s.Phone,
        OrderCount = s.Orders.Count,
        PendingShipments = s.Orders.Count(o => o.ShippedDate == null),
        TotalFreight = Math.Round(s.Orders.Sum(o => (double)o.Freight), 2),
        AverageFreight = Math.Round(s.Orders.Average(o => (double?)o.Freight) ?? 0, 2),
        AverageDaysToShip = Math.Round(s.Orders.Where(o => o.ShippedDate != null)
            .Average(o => (double?)(AppDbContext.JulianDay(o.ShippedDate!.Value) - AppDbContext.JulianDay(o.OrderDate))) ?? 0, 1),
        LateShipments = s.Orders.Count(o => o.ShippedDate > o.RequiredDate),
    });
}
